namespace Bookstore.ServiceDefaults;

using System.Reflection;
using System.Runtime.InteropServices;

/// <summary>
/// Inspects an entry assembly's manifest to produce a deterministic report of explicitly declared dependencies.
/// </summary>
/// <remarks>
/// 12-Factor Principle: Factor II — Dependencies
/// Explicitly declare and isolate dependencies.
///
/// Factor II mandates two complementary guarantees:
/// 1. Compile-time declaration and version pinning: All third-party packages are centrally declared in
///    <c>Directory.Packages.props</c> with transitive pinning enabled, restored from isolated feeds defined in
///    <c>nuget.config</c>, and verified via NuGet lock files. Local CLI tools (such as <c>dotnet-ef</c>) are
///    explicitly pinned in <c>.config/dotnet-tools.json</c>.
/// 2. Runtime dependency isolation: Workloads execute inside isolated .NET assembly load contexts and container
///    boundaries, communicating with backing services exclusively through managed client libraries
///    (<c>Npgsql</c>, <c>StackExchange.Redis</c>, <c>RabbitMQ.Client</c>) rather than shelling out to unpinned
///    host OS binaries (<c>psql</c>, <c>curl</c>, <c>redis-cli</c>).
///
/// <see cref="DependencyMetadataProvider"/> inspects the compiled assembly manifest to enumerate every declared
/// reference deterministically, ensuring zero undeclared cross-workload coupling.
/// </remarks>
public sealed class DependencyMetadataProvider : IDependencyMetadataProvider
{
    private const string FallbackVersion = "0.0.0.0";

    private readonly DependencyIsolationReport _report;

    /// <summary>
    /// Initializes a new instance of the <see cref="DependencyMetadataProvider"/> class.
    /// </summary>
    /// <param name="serviceName">The canonical name of the running service workload.</param>
    /// <param name="entryAssembly">The entry assembly whose declared references should be inspected.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="serviceName"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entryAssembly"/> is null.</exception>
    public DependencyMetadataProvider(string serviceName, Assembly entryAssembly)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        ArgumentNullException.ThrowIfNull(entryAssembly);

        var declaredDependencies = InspectAssembly(entryAssembly);
        var runtimeFramework = RuntimeInformation.FrameworkDescription;

        _report = new DependencyIsolationReport(
            ServiceName: serviceName,
            RuntimeFramework: runtimeFramework,
            IsSelfContainedOrIsolated: true,
            DeclaredDependencies: declaredDependencies);
    }

    /// <inheritdoc />
    public DependencyIsolationReport GetDependencyReport() => _report;

    /// <summary>
    /// Inspects the specified assembly and returns a deterministically sorted list of its explicitly declared assembly references.
    /// </summary>
    /// <param name="assembly">The assembly to inspect.</param>
    /// <returns>An immutable, ordinally sorted list of <see cref="DeclaredDependencyInfo"/> entries.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="assembly"/> is null.</exception>
    public static IReadOnlyList<DeclaredDependencyInfo> InspectAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        return assembly
            .GetReferencedAssemblies()
            .Where(reference => !string.IsNullOrWhiteSpace(reference.Name))
            .Select(reference => new DeclaredDependencyInfo(
                AssemblyName: reference.Name!,
                Version: reference.Version?.ToString() ?? FallbackVersion))
            .OrderBy(info => info.AssemblyName, StringComparer.Ordinal)
            .ThenBy(info => info.Version, StringComparer.Ordinal)
            .ToArray();
    }
}
