using System.Reflection;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bookstore.ArchitectureTests;

/// <summary>
/// Architectural verification tests for Factor II: Dependencies.
/// </summary>
/// <remarks>
/// 12-Factor Principle: Factor II — Dependencies
/// Explicitly declare and isolate dependencies.
///
/// These architectural tests enforce that the solution declares all NuGet dependencies centrally via
/// Directory.Packages.props without inline csproj versions, isolates package sources via nuget.config,
/// pins local CLI tools in .config/dotnet-tools.json, inspects explicit runtime assembly dependencies
/// via IDependencyMetadataProvider, and keeps workloads strictly isolated from one another.
/// </remarks>
public sealed class Factor02_DependenciesTests
{
    /// <summary>
    /// Verifies that Central Package Management, NuGet source isolation, and the local CLI tool manifest are explicitly configured.
    /// </summary>
    [Fact]
    public void CentralPackageManagement_AndNugetIsolation_AreExplicitlyConfigured()
    {
        var repoRoot = FindRepoRoot();
        var buildProps = Path.Combine(repoRoot, "Directory.Build.props");
        var packagesProps = Path.Combine(repoRoot, "Directory.Packages.props");
        var nugetConfig = Path.Combine(repoRoot, "nuget.config");
        var toolsManifest = Path.Combine(repoRoot, ".config", "dotnet-tools.json");

        Assert.True(File.Exists(packagesProps), "Directory.Packages.props must exist.");
        Assert.True(File.Exists(nugetConfig), "nuget.config must exist to isolate package feeds.");
        Assert.True(File.Exists(toolsManifest), ".config/dotnet-tools.json must declare CLI tools explicitly.");

        var buildDoc = XDocument.Load(buildProps);
        Assert.Equal("true", buildDoc.Descendants("RestorePackagesWithLockFile").Single().Value);

        var packagesDoc = XDocument.Load(packagesProps);
        Assert.Equal("true", packagesDoc.Descendants("ManagePackageVersionsCentrally").Single().Value);
        Assert.Equal("true", packagesDoc.Descendants("CentralPackageTransitivePinningEnabled").Single().Value);

        var nugetDoc = XDocument.Load(nugetConfig);
        Assert.NotEmpty(nugetDoc.Descendants("packageSources").Elements("clear"));
    }

    /// <summary>
    /// Verifies that no project file specifies inline PackageReference versions.
    /// </summary>
    [Fact]
    public void AllProjectFiles_UseCentralPackageManagementWithoutInlineVersions()
    {
        var repoRoot = FindRepoRoot();
        var csprojFiles = Directory.GetFiles(repoRoot, "*.csproj", SearchOption.AllDirectories);
        Assert.NotEmpty(csprojFiles);

        foreach (var csproj in csprojFiles)
        {
            var doc = XDocument.Load(csproj);
            var inlineVersions = doc.Descendants("PackageReference")
                .Where(pr => pr.Attribute("Version") is not null)
                .ToList();
            Assert.Empty(inlineVersions);
        }
    }

    /// <summary>
    /// Verifies that <see cref="Bookstore.ServiceDefaults.DependencyMetadataProvider"/> enumerates explicit assembly references and rejects blank service names.
    /// </summary>
    [Fact]
    public void DependencyMetadataProvider_EnumeratesExplicitAssemblyReferences_AndRejectsBlankServiceName()
    {
        Assert.Throws<ArgumentException>(() =>
            new Bookstore.ServiceDefaults.DependencyMetadataProvider("  ", typeof(Factor02_DependenciesTests).Assembly));
        Assert.Throws<ArgumentNullException>(() =>
            new Bookstore.ServiceDefaults.DependencyMetadataProvider("Catalog.Api", null!));

        var provider = new Bookstore.ServiceDefaults.DependencyMetadataProvider(
            "Catalog.Api",
            typeof(Catalog.Api.Program).Assembly);

        var report = provider.GetDependencyReport();
        Assert.Equal("Catalog.Api", report.ServiceName);
        Assert.True(report.IsSelfContainedOrIsolated);
        Assert.False(string.IsNullOrWhiteSpace(report.RuntimeFramework));
        Assert.Contains(report.DeclaredDependencies, d => d.AssemblyName == "Bookstore.Contracts");
        Assert.Contains(report.DeclaredDependencies, d => d.AssemblyName == "Bookstore.ServiceDefaults");
    }

    /// <summary>
    /// Verifies that all microservice workloads remain isolated from each other and share only Contracts and ServiceDefaults.
    /// </summary>
    [Fact]
    public void Workloads_RemainIsolatedFromEachOther_AndOnlyShareContractsAndServiceDefaults()
    {
        var workloads = new[]
        {
            typeof(Catalog.Api.Program).Assembly,
            typeof(Ordering.Api.Program).Assembly,
            typeof(Fulfillment.Worker.Program).Assembly
        };

        var workloadNames = workloads.Select(a => a.GetName().Name!).ToHashSet(StringComparer.Ordinal);

        foreach (var workload in workloads)
        {
            var referencedWorkloads = workload.GetReferencedAssemblies()
                .Select(a => a.Name!)
                .Where(name => workloadNames.Contains(name))
                .ToList();

            Assert.Empty(referencedWorkloads);
        }
    }

    /// <summary>
    /// Verifies that <see cref="Bookstore.ServiceDefaults.DependencyMetadataProvider.InspectAssembly"/> validates null arguments, sorts dependencies deterministically, and formats versions cleanly (including fallback to "0.0.0.0" when Version is null).
    /// </summary>
    [Fact]
    public void InspectAssembly_ValidatesNullAndReturnsDeterministicallySortedDependencies()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Bookstore.ServiceDefaults.DependencyMetadataProvider.InspectAssembly(null!));

        var dependencies = Bookstore.ServiceDefaults.DependencyMetadataProvider.InspectAssembly(typeof(Catalog.Api.Program).Assembly);
        Assert.NotEmpty(dependencies);

        var names = dependencies.Select(d => d.AssemblyName).ToList();
        var sortedNames = names.OrderBy(n => n, StringComparer.Ordinal).ToList();
        Assert.Equal(sortedNames, names);
        Assert.All(dependencies, d => Assert.False(string.IsNullOrWhiteSpace(d.Version)));

        var stubDependencies = Bookstore.ServiceDefaults.DependencyMetadataProvider.InspectAssembly(new StubAssemblyWithNullVersion());
        var single = Assert.Single(stubDependencies);
        Assert.Equal("Unversioned.Dependency", single.AssemblyName);
        Assert.Equal("0.0.0.0", single.Version);
    }

    private sealed class StubAssemblyWithNullVersion : Assembly
    {
        public override AssemblyName[] GetReferencedAssemblies() =>
        [
            new AssemblyName("Unversioned.Dependency") { Version = null }
        ];
    }

    /// <summary>
    /// Verifies that <see cref="Bookstore.ServiceDefaults.CodebaseEndpointExtensions.AddCodebaseMetadata"/> registers <see cref="Bookstore.ServiceDefaults.IDependencyMetadataProvider"/> in the service collection.
    /// </summary>
    [Fact]
    public void AddCodebaseMetadata_RegistersDependencyMetadataProvider_InServiceCollection()
    {
        var services = new ServiceCollection();
        Bookstore.ServiceDefaults.CodebaseEndpointExtensions.AddCodebaseMetadata(
            services,
            "Ordering.Api",
            typeof(Ordering.Api.Program).Assembly);

        using var serviceProvider = services.BuildServiceProvider();
        var provider = serviceProvider.GetRequiredService<Bookstore.ServiceDefaults.IDependencyMetadataProvider>();
        var report = provider.GetDependencyReport();

        Assert.Equal("Ordering.Api", report.ServiceName);
        Assert.Contains(report.DeclaredDependencies, d => d.AssemblyName == "Bookstore.Contracts");
    }

    /// <summary>
    /// Verifies that XML documentation for <c>Bookstore.ServiceDefaults</c> includes Factor II remarks.
    /// </summary>
    [Fact]
    public void ServiceDefaults_GenerateXmlDocumentationWithFactor02Remarks()
    {
        var xmlPath = Path.Combine(AppContext.BaseDirectory, "Bookstore.ServiceDefaults.xml");
        Assert.True(File.Exists(xmlPath), "XML documentation file for Bookstore.ServiceDefaults must be emitted on build.");
        var xmlContent = File.ReadAllText(xmlPath);
        Assert.Contains("12-Factor Principle: Factor II — Dependencies", xmlContent);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "TwelveFactor.Bookstore.sln")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
