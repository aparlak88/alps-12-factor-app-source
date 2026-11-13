using Bookstore.ServiceDefaults.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Bookstore.ServiceDefaults.BackingServices;

/// <summary>
/// Defines a connectivity probe for an attached backing service resource.
/// </summary>
/// <remarks>
/// 12-Factor Principle: Factor IV — Backing Services
/// Treat backing services as attached resources.
///
/// Because attached resources (databases, caches, message brokers) can be attached and detached at runtime or
/// experience transient network failures, workloads verify connectivity through uniform <see cref="IAttachedResourceProbe"/>
/// abstractions rather than hardcoding environment-specific health assumptions.
/// </remarks>
public interface IAttachedResourceProbe
{
    /// <summary>
    /// Gets the credential-redacted <see cref="AttachedResourceDescriptor"/> identifying the backing resource under test.
    /// </summary>
    AttachedResourceDescriptor Descriptor { get; }

    /// <summary>
    /// Asynchronously verifies whether the attached backing service is reachable and responsive.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns><see langword="true"/> if the attached resource responded cleanly; otherwise, <see langword="false"/>.</returns>
    Task<bool> CheckConnectivityAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Evaluates the health of all attached backing service resources registered via <see cref="IAttachedResourceProbe"/>
/// or configured through <see cref="BookstoreConfigOptions"/>.
/// </summary>
/// <remarks>
/// 12-Factor Principle: Factor IV — Backing Services
/// Treat backing services as attached resources.
///
/// When an attached resource such as PostgreSQL, Redis, or RabbitMQ becomes unreachable or is in the middle of being
/// detached and replaced, <see cref="AttachedResourceHealthCheck"/> catches connectivity failures and returns
/// <see cref="HealthStatus.Unhealthy"/> with structured resource metadata instead of crashing the host process with
/// unhandled socket exceptions.
/// </remarks>
public sealed class AttachedResourceHealthCheck : IHealthCheck
{
    private readonly IReadOnlyList<IAttachedResourceProbe> _probes;
    private readonly IOptions<BookstoreConfigOptions> _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="AttachedResourceHealthCheck"/> class.
    /// </summary>
    /// <param name="probes">The collection of registered <see cref="IAttachedResourceProbe"/> instances.</param>
    /// <param name="options">The environment-bound <see cref="BookstoreConfigOptions"/> used to summarize attached resources.</param>
    public AttachedResourceHealthCheck(
        IEnumerable<IAttachedResourceProbe> probes,
        IOptions<BookstoreConfigOptions> options)
    {
        ArgumentNullException.ThrowIfNull(probes);
        ArgumentNullException.ThrowIfNull(options);

        _probes = probes.ToList();
        _options = options;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>(StringComparer.Ordinal);
        var failedResources = new List<string>();

        if (_probes.Count == 0)
        {
            var descriptors = BackingServiceHealthChecksExtensions.BuildDescriptorsFromOptions(_options.Value);
            foreach (var descriptor in descriptors)
            {
                data[descriptor.Name] = $"{descriptor.Kind}:{descriptor.EndpointSummary}";
                if (string.IsNullOrWhiteSpace(descriptor.EndpointSummary) ||
                    string.Equals(descriptor.EndpointSummary, "unconfigured", StringComparison.OrdinalIgnoreCase))
                {
                    failedResources.Add(descriptor.Name);
                }
            }
        }
        else
        {
            foreach (var probe in _probes)
            {
                var descriptor = probe.Descriptor;
                try
                {
                    var isReachable = await probe.CheckConnectivityAsync(cancellationToken).ConfigureAwait(false);
                    data[descriptor.Name] = isReachable
                        ? $"healthy ({descriptor.EndpointSummary})"
                        : $"unreachable ({descriptor.EndpointSummary})";

                    if (!isReachable)
                    {
                        failedResources.Add(descriptor.Name);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    data[descriptor.Name] = $"error ({descriptor.EndpointSummary}): {ex.GetType().Name}";
                    failedResources.Add(descriptor.Name);
                }
            }
        }

        if (failedResources.Count > 0)
        {
            return HealthCheckResult.Unhealthy(
                description: $"Attached backing service probe failed for: {string.Join(", ", failedResources)}.",
                data: data);
        }

        return HealthCheckResult.Healthy(
            description: "All attached backing service resources are configured and reachable.",
            data: data);
    }
}
