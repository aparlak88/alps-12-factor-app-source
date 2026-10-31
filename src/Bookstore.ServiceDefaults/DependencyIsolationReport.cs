namespace Bookstore.ServiceDefaults;

/// <summary>
/// Captures the runtime dependency isolation report for a running microservice workload.
/// </summary>
/// <remarks>
/// 12-Factor Principle: Factor II — Dependencies
/// Explicitly declare and isolate dependencies.
///
/// Factor II requires both explicit compile-time dependency declaration (via NuGet Central Package Management
/// in <c>Directory.Packages.props</c> and deterministic lock files) and runtime process/container dependency
/// isolation (bundling required libraries within the application container and never shelling out to implicit
/// host OS utilities such as <c>curl</c>, <c>psql</c>, or <c>redis-cli</c>).
///
/// <see cref="DependencyIsolationReport"/> surfaces the service identity, target runtime framework, isolation
/// status, and the deterministic list of declared assembly references for operational and architectural auditing.
/// </remarks>
/// <param name="ServiceName">The canonical name of the microservice workload.</param>
/// <param name="RuntimeFramework">The target .NET framework description under which the process is executing.</param>
/// <param name="IsSelfContainedOrIsolated">Indicates whether the workload executes within an isolated dependency context.</param>
/// <param name="DeclaredDependencies">The deterministically ordered collection of explicitly declared assembly dependencies.</param>
public sealed record DependencyIsolationReport(
    string ServiceName,
    string RuntimeFramework,
    bool IsSelfContainedOrIsolated,
    IReadOnlyList<DeclaredDependencyInfo> DeclaredDependencies);
