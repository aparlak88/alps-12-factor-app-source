using Bookstore.ServiceDefaults.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bookstore.ServiceDefaults.BackingServices;

/// <summary>
/// Provides dependency injection registration, endpoint summarization, and HTTP health/metadata endpoints
/// for Factor IV attached backing service resources.
/// </summary>
/// <remarks>
/// 12-Factor Principle: Factor IV — Backing Services
/// Treat backing services as attached resources.
///
/// The code for a twelve-factor app makes no distinction between local and third-party backing services.
/// To the application, both are attached resources accessed via a URL or connection locator stored in configuration
/// (Factor III). By summarizing and probing PostgreSQL, Redis, and RabbitMQ purely through <see cref="BookstoreConfigOptions"/>,
/// a deployment can swap a local OrbStack Docker container (<c>docker-compose.backing.yml</c>) for Amazon RDS,
/// Amazon ElastiCache, or CloudAMQP by updating environment variables without modifying a single line of application code.
/// </remarks>
public static class BackingServiceHealthChecksExtensions
{
    /// <summary>
    /// Registers the <see cref="AttachedResourceHealthCheck"/> under the <c>"attached-resources"</c> readiness check
    /// tag (<c>"ready"</c>) in the service collection.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to register backing service health checks with.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for chaining.</returns>
    public static IServiceCollection AddAttachedResourceProbes(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHealthChecks()
            .AddCheck<AttachedResourceHealthCheck>("attached-resources", tags: ["ready"]);

        return services;
    }

    /// <summary>
    /// Maps the Factor IV backing service inspection endpoint (<c>GET /api/backing-services</c>),
    /// the liveness probe (<c>GET /health/live</c>), and the readiness probe (<c>GET /health/ready</c>).
    /// </summary>
    /// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/> to map the endpoints onto.</param>
    /// <returns>The same <see cref="IEndpointRouteBuilder"/> instance for chaining.</returns>
    public static IEndpointRouteBuilder MapAttachedResourceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/api/backing-services", GetAttachedResources)
            .WithName("GetAttachedResources")
            .WithSummary("Lists currently attached Factor IV backing service descriptors.")
            .WithDescription("Returns credential-redacted endpoint summaries for PostgreSQL, Redis, and RabbitMQ derived from environment configuration.");

        endpoints.MapGet("/health/live", () => TypedResults.Ok(new LivenessStatusResponse("Healthy")))
            .WithName("GetLivenessProbe")
            .WithSummary("Executes the process liveness probe.")
            .WithDescription("Returns 200 OK when the application process is alive and able to serve HTTP requests.");

        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("ready")
        });

        return endpoints;
    }

    /// <summary>
    /// Builds a typed <see cref="Ok{TValue}"/> HTTP response containing the list of <see cref="AttachedResourceDescriptor"/>
    /// instances resolved from the current <see cref="BookstoreConfigOptions"/>.
    /// </summary>
    /// <param name="options">The resolved <see cref="IOptions{TOptions}"/> for <see cref="BookstoreConfigOptions"/>.</param>
    /// <returns>An <see cref="Ok{TValue}"/> result containing the attached resource descriptors.</returns>
    public static Ok<IReadOnlyList<AttachedResourceDescriptor>> GetAttachedResources(
        IOptions<BookstoreConfigOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return TypedResults.Ok(BuildDescriptorsFromOptions(options.Value));
    }

    /// <summary>
    /// Projects the configured PostgreSQL, Redis, and RabbitMQ connection handles on <see cref="BookstoreConfigOptions"/>
    /// into credential-redacted <see cref="AttachedResourceDescriptor"/> records.
    /// </summary>
    /// <param name="options">The <see cref="BookstoreConfigOptions"/> instance containing backing service connection strings and URIs.</param>
    /// <returns>An immutable list of three <see cref="AttachedResourceDescriptor"/> items representing the attached backing services.</returns>
    public static IReadOnlyList<AttachedResourceDescriptor> BuildDescriptorsFromOptions(
        BookstoreConfigOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return
        [
            new AttachedResourceDescriptor(
                "catalog-postgres",
                "postgresql",
                SummarizePostgresEndpoint(options.Postgres?.ConnectionString)),
            new AttachedResourceDescriptor(
                "basket-redis",
                "redis",
                SummarizeRedisEndpoint(options.Redis?.ConnectionString)),
            new AttachedResourceDescriptor(
                "orders-rabbitmq",
                "rabbitmq",
                SummarizeRabbitMqEndpoint(options.RabbitMq?.Uri))
        ];
    }

    private static string SummarizePostgresEndpoint(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return "unconfigured";
        }

        string? host = null;
        string port = "5432";
        string? database = null;

        var segments = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var segment in segments)
        {
            var separatorIndex = segment.IndexOf('=', StringComparison.Ordinal);
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = segment[..separatorIndex].Trim();
            var value = segment[(separatorIndex + 1)..].Trim();

            if (key.Equals("Host", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("Server", StringComparison.OrdinalIgnoreCase))
            {
                host = value;
            }
            else if (key.Equals("Port", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(value))
            {
                port = value;
            }
            else if (key.Equals("Database", StringComparison.OrdinalIgnoreCase) ||
                     key.Equals("Initial Catalog", StringComparison.OrdinalIgnoreCase))
            {
                database = value;
            }
        }

        if (string.IsNullOrWhiteSpace(host))
        {
            return "unconfigured";
        }

        return string.IsNullOrWhiteSpace(database)
            ? $"{host}:{port}"
            : $"{host}:{port}/{database}";
    }

    private static string SummarizeRedisEndpoint(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return "unconfigured";
        }

        var parts = connectionString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            if (part.Contains('=', StringComparison.Ordinal))
            {
                continue;
            }

            var endpoint = part;
            var atIndex = endpoint.LastIndexOf('@');
            if (atIndex >= 0 && atIndex + 1 < endpoint.Length)
            {
                endpoint = endpoint[(atIndex + 1)..];
            }

            return endpoint;
        }

        return "unconfigured";
    }

    private static string SummarizeRabbitMqEndpoint(string? uriString)
    {
        if (string.IsNullOrWhiteSpace(uriString))
        {
            return "unconfigured";
        }

        if (Uri.TryCreate(uriString, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
        {
            var port = uri.Port > 0
                ? uri.Port
                : (string.Equals(uri.Scheme, "amqps", StringComparison.OrdinalIgnoreCase) ? 5671 : 5672);

            var vhost = uri.AbsolutePath.Trim('/');
            return string.IsNullOrWhiteSpace(vhost)
                ? $"{uri.Host}:{port}/"
                : $"{uri.Host}:{port}/{vhost}";
        }

        return "unconfigured";
    }
}

/// <summary>
/// Represents the JSON payload returned by the <c>GET /health/live</c> liveness endpoint.
/// </summary>
/// <remarks>
/// 12-Factor Principle: Factor IV — Backing Services
/// Treat backing services as attached resources.
///
///Separates process liveness (<c>/health/live</c>) from attached resource readiness (<c>/health/ready</c>) so container
/// orchestrators know whether the process itself is alive even when an attached backing service is being swapped.
/// </remarks>
/// <param name="Status">The health status string (for example, <c>"Healthy"</c>).</param>
public sealed record LivenessStatusResponse(string Status);
