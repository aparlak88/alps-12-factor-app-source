using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bookstore.ServiceDefaults.Configuration;

/// <summary>
/// Provides dependency injection registration and HTTP diagnostics endpoint extensions for Twelve-Factor Bookstore environment-backed configuration.
/// </summary>
/// <remarks>
/// 12-Factor Principle: Factor III — Config
/// Store config in the environment.
///
/// Passes the Factor III open-source litmus test ("could this codebase be open-sourced right now without compromising
/// any credentials or environment URLs?") by binding <see cref="BookstoreConfigOptions"/> from runtime environment
/// variables (where .NET translates double-underscore variables such as <c>Bookstore__Postgres__ConnectionString</c>
/// into hierarchical configuration keys <c>Bookstore:Postgres:ConnectionString</c>) and enforcing fail-fast startup
/// validation via <see cref="BookstoreOptionsValidator"/> and <c>ValidateOnStart()</c>. This eliminates static
/// per-environment profile files (<c>dev</c>, <c>staging</c>, <c>prod</c>) that fail to scale as deployments multiply.
/// </remarks>
public static class ConfigServiceCollectionExtensions
{
    /// <summary>
    /// The configuration or environment variable key (<c>"BOOKSTORE_ALLOW_FALLBACK_DEFAULTS"</c>) that enables in-memory
    /// local fallback defaults exclusively for isolated test hosts that do not provision all backing service variables.
    /// </summary>
    public const string AllowFallbackDefaultsKey = "BOOKSTORE_ALLOW_FALLBACK_DEFAULTS";

    /// <summary>
    /// The mask string (<c>"***REDACTED***"</c>) substituted for sensitive connection strings and broker URIs in diagnostic responses.
    /// </summary>
    public const string RedactedSecretMask = "***REDACTED***";

    /// <summary>
    /// Registers <see cref="BookstoreConfigOptions"/> bound to the <see cref="BookstoreConfigOptions.SectionName"/>
    /// configuration section and enables fail-fast startup validation via <see cref="BookstoreOptionsValidator"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to register configuration options with.</param>
    /// <param name="configuration">The application <see cref="IConfiguration"/> containing environment-supplied settings.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for chaining.</returns>
    public static IServiceCollection AddBookstoreEnvironmentConfig(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IValidateOptions<BookstoreConfigOptions>, BookstoreOptionsValidator>();

        var optionsBuilder = services.AddOptions<BookstoreConfigOptions>();

        if (IsFallbackDefaultsAllowed(configuration))
        {
            optionsBuilder.Configure(ApplyFallbackDefaults);
        }

        optionsBuilder
            .Bind(configuration.GetSection(BookstoreConfigOptions.SectionName))
            .ValidateOnStart();

        return services;
    }

    /// <summary>
    /// Maps the <c>GET /api/config/diagnostics</c> endpoint returning a <see cref="RedactedConfigDiagnostics"/> payload
    /// that exposes non-sensitive operational configuration while masking all connection strings and broker credentials
    /// with <see cref="RedactedSecretMask"/>.
    /// </summary>
    /// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/> to map the diagnostics endpoint onto.</param>
    /// <returns>The same <see cref="IEndpointRouteBuilder"/> instance for chaining.</returns>
    public static IEndpointRouteBuilder MapConfigDiagnosticsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/api/config/diagnostics", GetConfigDiagnostics)
            .WithName("GetConfigDiagnostics")
            .WithSummary("Retrieves redacted runtime configuration diagnostics.")
            .WithDescription("Returns Factor III environment configuration metadata with all sensitive connection strings and broker URIs redacted.");

        return endpoints;
    }

    /// <summary>
    /// Builds a typed <see cref="Ok{TValue}"/> HTTP result containing <see cref="RedactedConfigDiagnostics"/> from the resolved <see cref="BookstoreConfigOptions"/>.
    /// </summary>
    /// <param name="options">The resolved <see cref="IOptions{TOptions}"/> for <see cref="BookstoreConfigOptions"/>.</param>
    /// <returns>An <see cref="Ok{TValue}"/> response with redacted configuration diagnostics.</returns>
    public static Ok<RedactedConfigDiagnostics> GetConfigDiagnostics(IOptions<BookstoreConfigOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return TypedResults.Ok(RedactedConfigDiagnostics.FromOptions(options.Value));
    }

    private static bool IsFallbackDefaultsAllowed(IConfiguration configuration)
    {
        var configFlag = configuration[AllowFallbackDefaultsKey];
        if (string.Equals(configFlag, "true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var envFlag = Environment.GetEnvironmentVariable(AllowFallbackDefaultsKey);
        return string.Equals(envFlag, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static void ApplyFallbackDefaults(BookstoreConfigOptions options)
    {
        options.EnvironmentTier = "local-fallback";
        options.Postgres.ConnectionString = "Host=localhost;Port=5432;Database=bookstore_catalog;Username=bookstore;Password=local-dev-fallback";
        options.Redis.ConnectionString = "localhost:6379";
        options.RabbitMq.Uri = "amqp://guest:guest@localhost:5672/";
    }
}

/// <summary>
/// Represents a safe, credential-redacted projection of <see cref="BookstoreConfigOptions"/> exposed by <c>GET /api/config/diagnostics</c>.
/// </summary>
/// <remarks>
/// 12-Factor Principle: Factor III — Config
/// Store config in the environment.
///
/// Passes the Factor III open-source and operational safety litmus test ("could this codebase or diagnostic endpoint be
/// inspected without compromising any credentials or environment secrets?") by replacing all sensitive connection strings
/// and broker URIs with <see cref="ConfigServiceCollectionExtensions.RedactedSecretMask"/> (<c>"***REDACTED***"</c>) while
/// preserving visibility into non-sensitive Kubernetes <c>ConfigMap</c> tuning parameters.
/// </remarks>
/// <param name="EnvironmentTier">The active deployment environment tier identifier.</param>
/// <param name="Postgres">Redacted PostgreSQL configuration diagnostics.</param>
/// <param name="Redis">Redacted Redis configuration diagnostics.</param>
/// <param name="RabbitMq">Redacted RabbitMQ configuration diagnostics.</param>
public sealed record RedactedConfigDiagnostics(
    string EnvironmentTier,
    RedactedPostgresDiagnostics Postgres,
    RedactedRedisDiagnostics Redis,
    RedactedRabbitMqDiagnostics RabbitMq)
{
    /// <summary>
    /// Creates a <see cref="RedactedConfigDiagnostics"/> projection from the supplied <see cref="BookstoreConfigOptions"/>,
    /// replacing all connection strings and broker URIs with <see cref="ConfigServiceCollectionExtensions.RedactedSecretMask"/>.
    /// </summary>
    /// <param name="options">The live <see cref="BookstoreConfigOptions"/> instance to redact.</param>
    /// <returns>A new <see cref="RedactedConfigDiagnostics"/> instance safe for diagnostic output.</returns>
    public static RedactedConfigDiagnostics FromOptions(BookstoreConfigOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new RedactedConfigDiagnostics(
            options.EnvironmentTier,
            new RedactedPostgresDiagnostics(
                ConfigServiceCollectionExtensions.RedactedSecretMask,
                options.Postgres?.MaxPoolSize ?? 0),
            new RedactedRedisDiagnostics(
                ConfigServiceCollectionExtensions.RedactedSecretMask,
                options.Redis?.InstancePrefix ?? string.Empty),
            new RedactedRabbitMqDiagnostics(
                ConfigServiceCollectionExtensions.RedactedSecretMask,
                options.RabbitMq?.OrderQueueName ?? string.Empty));
    }
}

/// <summary>
/// Represents redacted PostgreSQL configuration diagnostics with the connection string masked.
/// </summary>
/// <remarks>
/// 12-Factor Principle: Factor III — Config
/// Store config in the environment.
///
/// Ensures the Factor III litmus test ("could this codebase be open-sourced right now without compromising any credentials
/// or environment URLs?") extends to runtime observability by masking <see cref="ConnectionString"/> while reporting
/// <see cref="MaxPoolSize"/>.
/// </remarks>
/// <param name="ConnectionString">Always <c>"***REDACTED***"</c> to prevent credential leakage.</param>
/// <param name="MaxPoolSize">The configured PostgreSQL maximum connection pool size.</param>
public sealed record RedactedPostgresDiagnostics(string ConnectionString, int MaxPoolSize);

/// <summary>
/// Represents redacted Redis configuration diagnostics with the connection string masked.
/// </summary>
/// <remarks>
/// 12-Factor Principle: Factor III — Config
/// Store config in the environment.
///
/// Ensures the Factor III litmus test ("could this codebase be open-sourced right now without compromising any credentials
/// or environment URLs?") extends to runtime observability by masking <see cref="ConnectionString"/> while reporting
/// <see cref="InstancePrefix"/>.
/// </remarks>
/// <param name="ConnectionString">Always <c>"***REDACTED***"</c> to prevent credential leakage.</param>
/// <param name="InstancePrefix">The configured logical key namespace prefix for Redis entries.</param>
public sealed record RedactedRedisDiagnostics(string ConnectionString, string InstancePrefix);

/// <summary>
/// Represents redacted RabbitMQ configuration diagnostics with the broker URI masked.
/// </summary>
/// <remarks>
/// 12-Factor Principle: Factor III — Config
/// Store config in the environment.
///
/// Ensures the Factor III litmus test ("could this codebase be open-sourced right now without compromising any credentials
/// or environment URLs?") extends to runtime observability by masking <see cref="Uri"/> while reporting
/// <see cref="OrderQueueName"/>.
/// </remarks>
/// <param name="Uri">Always <c>"***REDACTED***"</c> to prevent credential leakage.</param>
/// <param name="OrderQueueName">The configured RabbitMQ queue name for order-placed events.</param>
public sealed record RedactedRabbitMqDiagnostics(string Uri, string OrderQueueName);
