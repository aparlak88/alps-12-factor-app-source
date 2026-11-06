using Microsoft.Extensions.Options;

namespace Bookstore.ServiceDefaults.Configuration;

/// <summary>
/// Validates <see cref="BookstoreConfigOptions"/> at application startup to guarantee all required environment variables are present, well-formed, and free of hardcoded secret placeholders.
/// </summary>
/// <remarks>
/// 12-Factor Principle: Factor III — Config
/// Store config in the environment.
///
/// The litmus test for Factor III is: "could this codebase be open-sourced right now without compromising any credentials
/// or environment URLs?" Because all credentials and backing service endpoints are stripped from source-controlled files,
/// a workload relies entirely on environment variables injected by the execution platform (such as
/// <c>Bookstore__Postgres__ConnectionString</c>, which .NET automatically maps to <c>Bookstore:Postgres:ConnectionString</c>).
///
/// <see cref="BookstoreOptionsValidator"/> enforces fail-fast validation on startup (<c>ValidateOnStart()</c>) so that
/// any missing, malformed, or placeholder-tainted environment variable immediately halts host bootstrapping with a clear
/// diagnostic message before the process accepts HTTP traffic or processes queue messages.
///
/// Grouping configuration into named profile files (<c>dev</c>, <c>staging</c>, <c>prod</c>) fails as deployments scale
/// because new environments (ephemeral PR previews, staging clones, regional failovers, developer containers) multiply
/// combinatorially and tempt engineers to commit environment-specific URLs or fallback credentials into the repository.
/// </remarks>
public sealed class BookstoreOptionsValidator : IValidateOptions<BookstoreConfigOptions>
{
    private const string HardcodedSecretSentinel = "CONTAINS_HARDCODED_SECRET";

    /// <summary>
    /// Validates a specific named options instance (or all when <paramref name="name"/> is <see langword="null"/> or default).
    /// </summary>
    /// <param name="name">The name of the options instance being validated.</param>
    /// <param name="options">The <see cref="BookstoreConfigOptions"/> instance to validate.</param>
    /// <returns>A <see cref="ValidateOptionsResult"/> indicating success or listing every validation failure.</returns>
    public ValidateOptionsResult Validate(string? name, BookstoreConfigOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.EnvironmentTier))
        {
            failures.Add(
                "Bookstore:EnvironmentTier (env var Bookstore__EnvironmentTier) is required and must not be empty or whitespace.");
        }

        if (options.Postgres is null ||
            string.IsNullOrWhiteSpace(options.Postgres.ConnectionString) ||
            options.Postgres.ConnectionString.Contains(HardcodedSecretSentinel, StringComparison.OrdinalIgnoreCase))
        {
            failures.Add(
                "Bookstore:Postgres.ConnectionString (env var Bookstore__Postgres__ConnectionString) is required and must not contain hardcoded secrets.");
        }

        if (options.Postgres is null || options.Postgres.MaxPoolSize <= 0 || options.Postgres.MaxPoolSize > 500)
        {
            failures.Add(
                "Bookstore:Postgres.MaxPoolSize (env var Bookstore__Postgres__MaxPoolSize) must be between 1 and 500.");
        }

        if (options.Redis is null ||
            string.IsNullOrWhiteSpace(options.Redis.ConnectionString) ||
            options.Redis.ConnectionString.Contains(HardcodedSecretSentinel, StringComparison.OrdinalIgnoreCase))
        {
            failures.Add(
                "Bookstore:Redis.ConnectionString (env var Bookstore__Redis__ConnectionString) is required and must not contain hardcoded secrets.");
        }

        if (options.Redis is null || string.IsNullOrWhiteSpace(options.Redis.InstancePrefix))
        {
            failures.Add(
                "Bookstore:Redis.InstancePrefix (env var Bookstore__Redis__InstancePrefix) is required and must not be empty or whitespace.");
        }

        if (options.RabbitMq is null ||
            string.IsNullOrWhiteSpace(options.RabbitMq.Uri) ||
            options.RabbitMq.Uri.Contains(HardcodedSecretSentinel, StringComparison.OrdinalIgnoreCase) ||
            !IsValidAmqpUri(options.RabbitMq.Uri))
        {
            failures.Add(
                "Bookstore:RabbitMq.Uri (env var Bookstore__RabbitMq__Uri) must be a valid absolute amqp:// or amqps:// URI and must not contain hardcoded secrets.");
        }

        if (options.RabbitMq is null || string.IsNullOrWhiteSpace(options.RabbitMq.OrderQueueName))
        {
            failures.Add(
                "Bookstore:RabbitMq.OrderQueueName (env var Bookstore__RabbitMq__OrderQueueName) is required and must not be empty or whitespace.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }

    private static bool IsValidAmqpUri(string rawUri)
    {
        if (!Uri.TryCreate(rawUri, UriKind.Absolute, out var parsedUri))
        {
            return false;
        }

        return string.Equals(parsedUri.Scheme, "amqp", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(parsedUri.Scheme, "amqps", StringComparison.OrdinalIgnoreCase);
    }
}
