using System.Xml.Linq;
using Xunit;

namespace Bookstore.ArchitectureTests;

public sealed class Factor02_DependenciesTests
{
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
