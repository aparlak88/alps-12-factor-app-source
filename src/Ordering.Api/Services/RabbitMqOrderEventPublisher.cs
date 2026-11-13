using System.Text.Json;
using Bookstore.Contracts;
using Bookstore.ServiceDefaults.Configuration;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Ordering.Api.Services;

/// <summary>
/// RabbitMQ implementation of <see cref="IOrderEventPublisher"/> that publishes <see cref="OrderPlacedEvent"/> messages
/// to the broker attached via <see cref="BookstoreConfigOptions"/>.
/// </summary>
/// <remarks>
/// 12-Factor Principle: Factor IV — Backing Services
/// Treat backing services as attached resources.
///
/// <see cref="RabbitMqOrderEventPublisher"/> treats the RabbitMQ message broker as an attached resource whose
/// AMQP/AMQPS URI (<c>Bookstore__RabbitMq__Uri</c>) and target queue (<c>Bookstore__RabbitMq__OrderQueueName</c>)
/// are injected from environment configuration via <see cref="IOptions{TOptions}"/> of <see cref="BookstoreConfigOptions"/>.
/// A deployment can switch between a local RabbitMQ container in <c>docker-compose.backing.yml</c> and a managed
/// CloudAMQP or Amazon MQ broker solely by updating environment configuration with zero code changes.
/// </remarks>
public sealed class RabbitMqOrderEventPublisher : IOrderEventPublisher
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly BookstoreConfigOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="RabbitMqOrderEventPublisher"/> class bound to the environment-configured RabbitMQ resource.
    /// </summary>
    /// <param name="options">The strongly-typed <see cref="BookstoreConfigOptions"/> containing RabbitMQ broker settings.</param>
    public RabbitMqOrderEventPublisher(IOptions<BookstoreConfigOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Gets the AMQP/AMQPS broker URI resolved from <see cref="BookstoreConfigOptions"/>.
    /// </summary>
    public string ConfiguredBrokerUri => _options.RabbitMq.Uri;

    /// <summary>
    /// Gets the target order-placed queue name resolved from <see cref="BookstoreConfigOptions"/>.
    /// </summary>
    public string ConfiguredQueueName => _options.RabbitMq.OrderQueueName;

    /// <summary>
    /// Serializes an <see cref="OrderPlacedEvent"/> into a UTF-8 JSON byte array for AMQP transport.
    /// </summary>
    /// <param name="orderEvent">The <see cref="OrderPlacedEvent"/> instance to serialize.</param>
    /// <returns>A UTF-8 encoded JSON byte array representing the event.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="orderEvent"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="orderEvent"/> fails domain validation.</exception>
    public static byte[] SerializeEvent(OrderPlacedEvent orderEvent)
    {
        ArgumentNullException.ThrowIfNull(orderEvent);
        orderEvent.Validate();
        return JsonSerializer.SerializeToUtf8Bytes(orderEvent, SerializerOptions);
    }

    /// <summary>
    /// Deserializes a UTF-8 JSON byte span from an AMQP message payload back into an <see cref="OrderPlacedEvent"/>.
    /// </summary>
    /// <param name="utf8Json">The UTF-8 encoded JSON byte span.</param>
    /// <returns>The deserialized <see cref="OrderPlacedEvent"/> instance.</returns>
    /// <exception cref="JsonException">Thrown when the JSON payload cannot be deserialized into an <see cref="OrderPlacedEvent"/>.</exception>
    public static OrderPlacedEvent DeserializeEvent(ReadOnlySpan<byte> utf8Json)
    {
        var restored = JsonSerializer.Deserialize<OrderPlacedEvent>(utf8Json, SerializerOptions)
            ?? throw new JsonException("Failed to deserialize OrderPlacedEvent from UTF-8 JSON payload.");

        restored.Validate();
        return restored;
    }

    /// <inheritdoc />
    public async Task PublishOrderPlacedAsync(OrderPlacedEvent orderEvent, CancellationToken ct = default)
    {
        var body = SerializeEvent(orderEvent);
        ct.ThrowIfCancellationRequested();

        var queueName = string.IsNullOrWhiteSpace(_options.RabbitMq.OrderQueueName)
            ? "bookstore.orders.placed"
            : _options.RabbitMq.OrderQueueName.Trim();

        var factory = new ConnectionFactory
        {
            Uri = new Uri(_options.RabbitMq.Uri, UriKind.Absolute)
        };

        await using var connection = await factory.CreateConnectionAsync(ct).ConfigureAwait(false);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct).ConfigureAwait(false);

        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: ct).ConfigureAwait(false);

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = orderEvent.OrderId.ToString("D"),
            Timestamp = new AmqpTimestamp(orderEvent.PlacedAtUtc.ToUnixTimeSeconds())
        };

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: queueName,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: ct).ConfigureAwait(false);
    }
}
