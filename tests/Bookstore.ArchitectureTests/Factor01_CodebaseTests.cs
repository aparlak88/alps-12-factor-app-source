using System.Reflection;
using System.Xml.Linq;
using Xunit;

namespace Bookstore.ArchitectureTests;

public sealed class Factor01_CodebaseTests
{
    [Fact]
    public void DirectoryBuildProps_EnforcesSingleCodebaseStandardsAndXmlDocumentation()
    {
        var repoRoot = FindRepoRoot();
        var propsPath = Path.Combine(repoRoot, "Directory.Build.props");
        Assert.True(File.Exists(propsPath), "Directory.Build.props must exist at repository root.");

        var doc = XDocument.Load(propsPath);
        Assert.Equal("net10.0", doc.Descendants("TargetFramework").Single().Value);
        Assert.Equal("true", doc.Descendants("GenerateDocumentationFile").Single().Value);
        Assert.Equal("true", doc.Descendants("TreatWarningsAsErrors").Single().Value);
        Assert.Equal("true", doc.Descendants("IncludeSourceRevisionInInformationalVersion").Single().Value);
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
