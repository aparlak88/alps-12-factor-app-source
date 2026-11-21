using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bookstore.ServiceDefaults.Release;

/// <summary>
/// Provides dependency injection registration and HTTP inspection endpoint extensions for Factor V immutable release metadata.
/// </summary>
/// <remarks>
/// 12-Factor Principle: Factor V — Build, Release, Run
/// Strictly separate build and run stages.
///
/// Because code can never be mutated at runtime without breaking repeatability across restarts and horizontal replicas,
/// every deployed workload represents an immutable composition of <c>Release = Build(Commit SHA) + Config(Environment)</c>.
/// Registering <see cref="IReleaseMetadataProvider"/> and exposing <c>GET /api/release</c> allows deployment tooling,
/// smoke tests, and operators to verify the exact immutable release identifier serving traffic and validate zero-rebuild
/// rollbacks to prior release IDs.
/// </remarks>
public static class ReleaseEndpointExtensions
{
    /// <summary>
    /// Registers <see cref="IReleaseMetadataProvider"/> implemented by <see cref="ReleaseMetadataProvider"/> as a singleton
    /// in the dependency injection container.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to configure.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    public static IServiceCollection AddReleaseMetadata(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IReleaseMetadataProvider, ReleaseMetadataProvider>();

        return services;
    }

    /// <summary>
    /// Maps the <c>GET /api/release</c> endpoint returning the workload's <see cref="ReleaseDescriptor"/>.
    /// </summary>
    /// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/> to map the release endpoint onto.</param>
    /// <returns>The same <see cref="IEndpointRouteBuilder"/> instance for fluent chaining.</returns>
    public static IEndpointRouteBuilder MapReleaseEndpoint(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/api/release", GetReleaseDescriptor)
            .WithName("GetReleaseDescriptor")
            .WithSummary("Retrieves Factor V immutable release identity metadata.")
            .WithDescription("Returns the immutable release descriptor combining build provenance (version and Git commit SHA), environment configuration tier, and release identifier.");

        return endpoints;
    }

    /// <summary>
    /// Produces a typed <see cref="Ok{TValue}"/> HTTP response containing the <see cref="ReleaseDescriptor"/> resolved
    /// from <see cref="IReleaseMetadataProvider"/>.
    /// </summary>
    /// <param name="provider">The registered <see cref="IReleaseMetadataProvider"/> instance.</param>
    /// <returns>An <see cref="Ok{TValue}"/> response wrapping the current <see cref="ReleaseDescriptor"/>.</returns>
    public static Ok<ReleaseDescriptor> GetReleaseDescriptor(IReleaseMetadataProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return TypedResults.Ok(provider.GetReleaseDescriptor());
    }
}
