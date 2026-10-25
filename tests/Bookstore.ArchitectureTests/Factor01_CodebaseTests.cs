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

    [Fact]
    public void SharedContracts_AreReferencedByAllWorkloads_AndValidateDomainInvariants()
    {
        var invalidBook = new Bookstore.Contracts.BookDto(Guid.NewGuid(), "978-0132350884", "Clean Code", "Robert C. Martin", -5m, 10);
        Assert.Throws<ArgumentOutOfRangeException>(() => invalidBook.Validate());

        var invalidStock = new Bookstore.Contracts.BookDto(Guid.NewGuid(), "978-0132350884", "Clean Code", "Robert C. Martin", 35m, -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => invalidStock.Validate());

        var emptyOrder = new Bookstore.Contracts.OrderPlacedEvent(Guid.NewGuid(), "cust-1", [], DateTimeOffset.UtcNow);
        Assert.Throws<ArgumentException>(() => emptyOrder.Validate());

        var whitespaceCustomerOrder = new Bookstore.Contracts.OrderPlacedEvent(Guid.NewGuid(), "   ", [new Bookstore.Contracts.BasketItemDto(Guid.NewGuid(), "Clean Code", 1, 35m)], DateTimeOffset.UtcNow);
        Assert.Throws<ArgumentException>(() => whitespaceCustomerOrder.Validate());

        var validBook = new Bookstore.Contracts.BookDto(Guid.NewGuid(), "978-0132350884", "Clean Code", "Robert C. Martin", 35m, 10);
        validBook.Validate();

        var item1 = new Bookstore.Contracts.BasketItemDto(Guid.NewGuid(), "Clean Code", 2, 30m);
        var item2 = new Bookstore.Contracts.BasketItemDto(Guid.NewGuid(), "Refactoring", 1, 40m);
        var basket = new Bookstore.Contracts.CustomerBasketDto("cust-1", [item1, item2]);
        Assert.Equal(100m, basket.TotalPrice);

        var validOrder = new Bookstore.Contracts.OrderPlacedEvent(Guid.NewGuid(), "cust-1", [item1], DateTimeOffset.UtcNow);
        validOrder.Validate();

        var metadata = new Bookstore.Contracts.CodebaseMetadata("Catalog.Api", "abcdef1", "1.0.0", "Development");
        Assert.Equal("Catalog.Api", metadata.ServiceName);
        Assert.Equal("abcdef1", metadata.GitCommitSha);
        Assert.Equal("1.0.0", metadata.BuildVersion);
        Assert.Equal("Development", metadata.EnvironmentName);
    }

    [Fact]
    public void SharedContracts_GenerateXmlDocumentationWithFactorRemarks()
    {
        var xmlPath = Path.Combine(AppContext.BaseDirectory, "Bookstore.Contracts.xml");
        Assert.True(File.Exists(xmlPath), "XML documentation file for Bookstore.Contracts must be emitted on build.");
        var xmlContent = File.ReadAllText(xmlPath);
        Assert.Contains("12-Factor Principle: Factor I", xmlContent);
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
