namespace Bookstore.ServiceDefaults;

using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Extension methods for configuring and exposing codebase revision tracking and multi-deploy metadata.
/// </summary>
/// <remarks>
/// 12-Factor Principle: Factor I — Codebase
/// One codebase tracked in revision control, many deploys.
/// 
/// These extensions satisfy Factor I by standardizing how microservices register their codebase identity
/// in dependency injection and expose it over HTTP endpoints (/api/codebase).
/// This allows deployment platforms, monitoring tools, and verification suites to audit the exact Git commit
/// and runtime environment across any deploy without diverging service implementations.
/// </remarks>
public static class CodebaseEndpointExtensions
{
    /// <summary>
    /// Registers codebase revision metadata services in the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="serviceName">The canonical name of the running service workload.</param>
    /// <param name="entryAssembly">The entry assembly from which to extract Git commit SHA and build version metadata.</param>
    /// <returns>The same service collection for fluent chaining.</returns>
    public static IServiceCollection AddCodebaseMetadata(this IServiceCollection services, string serviceName, Assembly entryAssembly)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        ArgumentNullException.ThrowIfNull(entryAssembly);

        services.AddSingleton<ICodebaseInfoProvider>(sp =>
        {
            var hostEnvironment = sp.GetService<IHostEnvironment>();
            var envName = hostEnvironment?.EnvironmentName;
            return new CodebaseInfoProvider(serviceName, entryAssembly, envName);
        });

        return services;
    }

    /// <summary>
    /// Maps the GET /api/codebase endpoint to expose Factor I codebase revision and deployment identity metadata.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder to configure.</param>
    /// <returns>The configured endpoint route builder for fluent chaining.</returns>
    public static IEndpointRouteBuilder MapCodebaseEndpoint(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/api/codebase", (ICodebaseInfoProvider provider) =>
        {
            return TypedResults.Ok(provider.GetMetadata());
        })
        .WithName("GetCodebaseMetadata")
        .WithSummary("Retrieves codebase revision and deployment environment metadata.")
        .WithDescription("Returns Factor I metadata (service name, Git commit SHA, build version, and environment name) for revision tracking across multiple deploys.");

        return endpoints;
    }
}
