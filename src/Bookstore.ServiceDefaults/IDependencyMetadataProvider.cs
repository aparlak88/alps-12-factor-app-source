namespace Bookstore.ServiceDefaults;

/// <summary>
/// Provides runtime inspection of explicitly declared assembly dependencies and process isolation metadata.
/// </summary>
/// <remarks>
/// 12-Factor Principle: Factor II — Dependencies
/// Explicitly declare and isolate dependencies.
///
/// This interface defines a standard contract for twelve-factor workloads to expose their declared compile-time
/// and runtime assembly dependencies. By making the dependency graph observable at runtime (e.g., via
/// <c>GET /api/dependencies</c>) and verifiable in architectural tests, the platform guarantees that workloads
/// remain strictly isolated from one another and do not rely on undeclared system-wide packages or cross-service
/// implementation coupling.
/// </remarks>
public interface IDependencyMetadataProvider
{
    /// <summary>
    /// Retrieves the dependency isolation report for the current running workload.
    /// </summary>
    /// <returns>A <see cref="DependencyIsolationReport"/> describing the workload's explicit dependency graph.</returns>
    DependencyIsolationReport GetDependencyReport();
}
