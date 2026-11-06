namespace Bookstore.ServiceDefaults.Configuration;

/// <summary>
/// Represents the strongly-typed, environment-injected configuration options for all Twelve-Factor Bookstore workloads.
/// </summary>
/// <remarks>
/// 12-Factor Principle: Factor III — Config
/// Store config in the environment.
///
/// Factor III defines configuration as everything that is likely to vary between deploys (staging, production,
/// developer environments, local OrbStack containers). This includes backing service credentials, connection strings,
/// and per-deploy resource tuning parameters.
///
/// The litmus test for Factor III compliance is: "could this codebase be open-sourced right now without compromising
/// any credentials or environment URLs?" By keeping <c>appsettings.json</c> restricted to non-sensitive structural
/// logging defaults and defaulting all connection strings here to <see cref="string.Empty"/>, the repository contains
/// zero secrets or environment-coupled endpoints.
///
/// In .NET, the environment variable configuration provider maps hierarchical keys delimited by double underscores
/// (<c>__</c>) directly into the configuration hierarchy—for example, the OS environment variable
/// <c>Bookstore__Postgres__ConnectionString</c> binds seamlessly to <c>Bookstore:Postgres:ConnectionString</c>
/// on <see cref="Postgres"/>.
///
/// Grouping configuration into named profile files (such as <c>appsettings.dev.json</c>, <c>appsettings.staging.json</c>,
/// <c>appsettings.prod.json</c>, or <c>appsettings.qa-eu-west.json</c>) fails as deployments scale: every new developer
/// sandbox, regional cluster, or ephemeral preview environment forces commits to source control and increases the risk
/// of leaking credentials across environments. Instead, <see cref="BookstoreOptionsValidator"/> enforces fail-fast
/// startup validation (<c>ValidateOnStart()</c>) over granular OS environment variables.
/// </remarks>
public sealed class BookstoreConfigOptions
{
    /// <summary>
    /// The canonical configuration section name (<c>"Bookstore"</c>) bound from environment variables prefixed with <c>Bookstore__</c>.
    /// </summary>
    public const string SectionName = "Bookstore";

    /// <summary>
    /// Gets or sets the deployment environment tier identifier (for example, <c>local-orbstack</c>, <c>staging</c>, or <c>production</c>).
    /// </summary>
    public string EnvironmentTier { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the PostgreSQL backing service connection and pool configuration.
    /// </summary>
    public PostgresOptions Postgres { get; set; } = new();

    /// <summary>
    /// Gets or sets the Redis backing service connection and key-prefix configuration.
    /// </summary>
    public RedisOptions Redis { get; set; } = new();

    /// <summary>
    /// Gets or sets the RabbitMQ message broker URI and queue configuration.
    /// </summary>
    public RabbitMqOptions RabbitMq { get; set; } = new();
}

/// <summary>
/// Represents PostgreSQL backing service configuration bound from <c>Bookstore__Postgres__*</c> environment variables.
/// </summary>
/// <remarks>
/// 12-Factor Principle: Factor III — Config
/// Store config in the environment.
///
/// Passes the Factor III open-source litmus test ("could this codebase be open-sourced right now without compromising
/// any credentials or environment URLs?") by defaulting <see cref="ConnectionString"/> to <see cref="string.Empty"/>
/// and requiring runtime injection via <c>Bookstore__Postgres__ConnectionString</c> (mapped by .NET to
/// <c>Bookstore:Postgres:ConnectionString</c>). Validated at startup by <see cref="BookstoreOptionsValidator"/> via
/// <c>ValidateOnStart()</c> rather than relying on fragile per-environment profile files (<c>dev</c>, <c>staging</c>, <c>prod</c>)
/// that fail to scale across deployments.
/// </remarks>
public sealed class PostgresOptions
{
    /// <summary>
    /// Gets or sets the PostgreSQL connection string injected from the <c>Bookstore__Postgres__ConnectionString</c> environment variable.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the maximum number of connections in the PostgreSQL connection pool (1 to 500). Defaults to <c>50</c>.
    /// </summary>
    public int MaxPoolSize { get; set; } = 50;
}

/// <summary>
/// Represents Redis backing service configuration bound from <c>Bookstore__Redis__*</c> environment variables.
/// </summary>
/// <remarks>
/// 12-Factor Principle: Factor III — Config
/// Store config in the environment.
///
/// Passes the Factor III open-source litmus test ("could this codebase be open-sourced right now without compromising
/// any credentials or environment URLs?") by defaulting <see cref="ConnectionString"/> to <see cref="string.Empty"/>
/// and requiring runtime injection via <c>Bookstore__Redis__ConnectionString</c> (mapped by .NET from double-underscore
/// environment variables such as <c>Bookstore__Postgres__ConnectionString</c> to <c>Bookstore:Postgres:ConnectionString</c>).
/// Validated at startup by <see cref="BookstoreOptionsValidator"/> via <c>ValidateOnStart()</c> instead of scattering
/// settings across <c>dev</c>, <c>staging</c>, and <c>prod</c> config files that fail as deployments scale.
/// </remarks>
public sealed class RedisOptions
{
    /// <summary>
    /// Gets or sets the Redis connection string injected from the <c>Bookstore__Redis__ConnectionString</c> environment variable.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the logical key namespace prefix for Redis entries. Defaults to <c>"bookstore:"</c>.
    /// </summary>
    public string InstancePrefix { get; set; } = "bookstore:";
}

/// <summary>
/// Represents RabbitMQ backing service configuration bound from <c>Bookstore__RabbitMq__*</c> environment variables.
/// </summary>
/// <remarks>
/// 12-Factor Principle: Factor III — Config
/// Store config in the environment.
///
/// Passes the Factor III open-source litmus test ("could this codebase be open-sourced right now without compromising
/// any credentials or environment URLs?") by defaulting <see cref="Uri"/> to <see cref="string.Empty"/> and requiring
/// runtime injection via <c>Bookstore__RabbitMq__Uri</c> (using the same .NET <c>__</c> to <c>:</c> mapping that binds
/// <c>Bookstore__Postgres__ConnectionString</c> to <c>Bookstore:Postgres:ConnectionString</c>). Validated at startup
/// by <see cref="BookstoreOptionsValidator"/> via <c>ValidateOnStart()</c> rather than maintaining static <c>dev</c>,
/// <c>staging</c>, and <c>prod</c> profile files that fail as deployments scale.
/// </remarks>
public sealed class RabbitMqOptions
{
    /// <summary>
    /// Gets or sets the AMQP/AMQPS broker URI injected from the <c>Bookstore__RabbitMq__Uri</c> environment variable.
    /// </summary>
    public string Uri { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the queue name used for publishing and consuming order-placed events. Defaults to <c>"bookstore.orders.placed"</c>.
    /// </summary>
    public string OrderQueueName { get; set; } = "bookstore.orders.placed";
}
