namespace Bookstore.ServiceDefaults;

using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Extension methods for configuring and exposing codebase revision tracking (Factor I) and explicit dependency isolation metadata (Factor II).
/// </summary>
/// <remarks>
/// 12-Factor Principle: Factor I — Codebase
/// One codebase tracked in revision control, many deploys.
///
/// 12-Factor Principle: Factor II — Dependencies
/// Explicitly declare and isolate dependencies.
/// 
/// These extensions satisfy Factor I and Factor II by standardizing how microservices register their codebase identity
/// and declared runtime assembly dependency graph in dependency injection and expose them over HTTP endpoints
/// (<c>/api/codebase</c> and <c>/api/dependencies</c>).
/// This allows deployment platforms, monitoring tools, and verification suites to audit the exact Git commit,
/// runtime environment, and isolated dependency graph across any deploy without diverging service implementations.
/// </remarks>
public static class CodebaseEndpointExtensions
{
    /// <summary>
    /// Registers codebase revision metadata and explicit dependency isolation services in the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="serviceName">The canonical name of the running service workload.</param>
    /// <param name="entryAssembly">The entry assembly from which to extract Git commit SHA, build version, and declared dependency metadata.</param>
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

        services.AddSingleton<IDependencyMetadataProvider>(
            new DependencyMetadataProvider(serviceName, entryAssembly));

        return services;
    }

    /// <summary>
    /// Maps the GET /api/codebase and GET /api/dependencies endpoints to expose Factor I codebase revision and Factor II dependency isolation metadata.
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

        endpoints.MapGet("/api/dependencies", (IDependencyMetadataProvider provider) =>
        {
            return TypedResults.Ok(provider.GetDependencyReport());
        })
        .WithName("GetDependencyIsolationReport")
        .WithSummary("Retrieves explicitly declared runtime assembly dependencies and isolation metadata.")
        .WithDescription("Returns Factor II dependency isolation metadata (service name, runtime framework, isolation flag, and declared assembly dependencies).");

        return endpoints;
    }
}
