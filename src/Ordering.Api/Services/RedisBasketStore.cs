using System.Text.Json;
using Bookstore.Contracts;
using Bookstore.ServiceDefaults.Configuration;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Ordering.Api.Services;

/// <summary>
/// Redis-backed implementation of <see cref="IBasketStore"/> that connects to an attached Redis resource configured via <see cref="BookstoreConfigOptions"/>.
/// </summary>
/// <remarks>
/// 12-Factor Principle: Factor IV — Backing Services
/// Treat backing services as attached resources.
///
/// <see cref="RedisBasketStore"/> treats the Redis cache/session store as an attached resource resolved exclusively
/// from <see cref="IOptions{TOptions}"/> of <see cref="BookstoreConfigOptions"/> (<c>Bookstore__Redis__ConnectionString</c>
/// and <c>Bookstore__Redis__InstancePrefix</c>). It uses lazy, resilient connection multiplexing (<c>AbortOnConnectFail = false</c>)
/// so that service registration and domain validation never eagerly block on TCP socket handshakes, and so the application
/// can seamlessly attach, detach, or swap between a local <c>redis:7.4-alpine</c> container and a managed cloud cluster
/// solely via environment configuration.
/// </remarks>
public sealed class RedisBasketStore : IBasketStore, IAsyncDisposable, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly BookstoreConfigOptions _options;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private IConnectionMultiplexer? _multiplexer;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisBasketStore"/> class bound to the environment-configured Redis resource.
    /// </summary>
    /// <param name="options">The strongly-typed <see cref="BookstoreConfigOptions"/> containing Redis connection settings.</param>
    public RedisBasketStore(IOptions<BookstoreConfigOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Gets the Redis connection string resolved from <see cref="BookstoreConfigOptions"/>.
    /// </summary>
    public string ConfiguredConnectionString => _options.Redis.ConnectionString;

    /// <summary>
    /// Gets the Redis key namespace prefix resolved from <see cref="BookstoreConfigOptions"/>.
    /// </summary>
    public string ConfiguredInstancePrefix => _options.Redis.InstancePrefix;

    /// <summary>
    /// Builds a canonical prefixed Redis key for a customer's shopping basket.
    /// </summary>
    /// <param name="instancePrefix">The configured Redis key prefix (defaults to <c>"bookstore:"</c> when blank).</param>
    /// <param name="customerId">The unique customer identifier.</param>
    /// <returns>The formatted Redis key string (for example, <c>"bookstore:basket:cust-42"</c>).</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="customerId"/> is null, empty, or whitespace.</exception>
    public static string BuildRedisKey(string instancePrefix, string customerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerId);

        var prefix = string.IsNullOrWhiteSpace(instancePrefix)
            ? "bookstore:"
            : instancePrefix.Trim();

        return $"{prefix}basket:{customerId.Trim()}";
    }

    /// <summary>
    /// Serializes a <see cref="CustomerBasketDto"/> into a UTF-8 JSON byte array for storage in Redis.
    /// </summary>
    /// <param name="basket">The shopping basket DTO to serialize.</param>
    /// <returns>A UTF-8 encoded JSON byte array representing the basket.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="basket"/> is null.</exception>
    public static byte[] SerializeBasket(CustomerBasketDto basket)
    {
        ArgumentNullException.ThrowIfNull(basket);
        return JsonSerializer.SerializeToUtf8Bytes(basket, SerializerOptions);
    }

    /// <summary>
    /// Deserializes a UTF-8 JSON byte span from Redis back into a <see cref="CustomerBasketDto"/>.
    /// </summary>
    /// <param name="utf8Json">The UTF-8 JSON payload read from Redis.</param>
    /// <returns>The deserialized <see cref="CustomerBasketDto"/>, or <see langword="null"/> if the payload is empty.</returns>
    public static CustomerBasketDto? DeserializeBasket(ReadOnlySpan<byte> utf8Json)
    {
        if (utf8Json.IsEmpty)
        {
            return null;
        }

        return JsonSerializer.Deserialize<CustomerBasketDto>(utf8Json, SerializerOptions);
    }

    /// <inheritdoc />
    public async Task<CustomerBasketDto?> GetBasketAsync(string customerId, CancellationToken ct = default)
    {
        var key = BuildRedisKey(_options.Redis.InstancePrefix, customerId);
        ct.ThrowIfCancellationRequested();

        var database = await GetDatabaseAsync(ct).ConfigureAwait(false);
        var payload = await database.StringGetAsync(key).ConfigureAwait(false);
        if (payload.IsNullOrEmpty)
        {
            return null;
        }

        byte[]? bytes = payload;
        return bytes is null ? null : DeserializeBasket(bytes);
    }

    /// <inheritdoc />
    public async Task<CustomerBasketDto> UpsertBasketAsync(CustomerBasketDto basket, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(basket);
        var key = BuildRedisKey(_options.Redis.InstancePrefix, basket.CustomerId);
        if (basket.Items is null)
        {
            throw new ArgumentException("Basket items collection cannot be null.", nameof(basket));
        }

        var payload = SerializeBasket(basket);
        ct.ThrowIfCancellationRequested();

        var database = await GetDatabaseAsync(ct).ConfigureAwait(false);
        await database.StringSetAsync(key, payload).ConfigureAwait(false);
        return basket;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteBasketAsync(string customerId, CancellationToken ct = default)
    {
        var key = BuildRedisKey(_options.Redis.InstancePrefix, customerId);
        ct.ThrowIfCancellationRequested();

        var database = await GetDatabaseAsync(ct).ConfigureAwait(false);
        return await database.KeyDeleteAsync(key).ConfigureAwait(false);
    }

    private async Task<IDatabase> GetDatabaseAsync(CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_multiplexer is { IsConnected: true })
        {
            return _multiplexer.GetDatabase();
        }

        await _connectionLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_multiplexer is null)
            {
                var configOptions = ConfigurationOptions.Parse(_options.Redis.ConnectionString);
                configOptions.AbortOnConnectFail = false;
                _multiplexer = await ConnectionMultiplexer.ConnectAsync(configOptions).ConfigureAwait(false);
            }

            return _multiplexer.GetDatabase();
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_multiplexer is not null)
        {
            await _multiplexer.DisposeAsync().ConfigureAwait(false);
            _multiplexer = null;
        }

        _connectionLock.Dispose();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _multiplexer?.Dispose();
        _multiplexer = null;
        _connectionLock.Dispose();
    }
}
