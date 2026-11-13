namespace Bookstore.ServiceDefaults.BackingServices;

/// <summary>
/// Represents a credential-redacted metadata descriptor for an attached backing service resource.
/// </summary>
/// <remarks>
/// 12-Factor Principle: Factor IV — Backing Services
/// Treat backing services as attached resources.
///
/// A backing service is any service the application consumes over the network as part of its normal operation
/// (such as PostgreSQL, Redis, or RabbitMQ). Under Factor IV, the application code makes no distinction between
/// a local container (e.g., running via <c>docker-compose.backing.yml</c> in OrbStack) and a managed third-party
/// cloud service (e.g., Amazon RDS, Amazon ElastiCache, or CloudAMQP).
///
/// Each backing service is treated as an attached resource identified purely by its configuration locator
/// (<c>Bookstore__Postgres__ConnectionString</c>, <c>Bookstore__Redis__ConnectionString</c>, <c>Bookstore__RabbitMq__Uri</c>).
/// <see cref="AttachedResourceDescriptor"/> exposes the logical resource name, resource kind, and sanitized
/// endpoint summary (host, port, and database/virtual-host) without leaking passwords or secret tokens, making it
/// safe to inspect resource attachment state at runtime via <c>GET /api/backing-services</c>.
/// </remarks>
/// <param name="Name">The logical resource handle within the Twelve-Factor Bookstore architecture (for example, <c>"catalog-postgres"</c>, <c>"basket-redis"</c>, or <c>"orders-rabbitmq"</c>).</param>
/// <param name="Kind">The backing service protocol or technology classification (for example, <c>"postgresql"</c>, <c>"redis"</c>, or <c>"rabbitmq"</c>).</param>
/// <param name="EndpointSummary">The credential-stripped network locator summary (for example, <c>"pg-prod.internal:5432/catalog"</c>) derived from environment configuration.</param>
public sealed record AttachedResourceDescriptor(
    string Name,
    string Kind,
    string EndpointSummary);
