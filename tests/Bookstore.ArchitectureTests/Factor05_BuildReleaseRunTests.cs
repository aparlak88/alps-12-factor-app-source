using System.Net;
using System.Net.Http.Json;
using Bookstore.ServiceDefaults;
using Bookstore.ServiceDefaults.Configuration;
using Bookstore.ServiceDefaults.Release;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Bookstore.ArchitectureTests;

/// <summary>
/// Architectural verification tests for Factor V: Build, Release, Run.
/// </summary>
/// <remarks>
/// 12-Factor Principle: Factor V — Build, Release, Run
/// Strictly separate build and run stages.
///
/// Code can never be mutated at runtime because any ad-hoc change in the run stage cannot be propagated back to the
/// build stage, destroying repeatability and auditability. Instead, every deployment is composed deterministically as
/// Release = Build(Commit SHA) + Config(Environment), producing an immutable release identified by a unique ReleaseId
/// (timestamp-SHA or semantic-SHA). Because builds and releases are immutable, rolling back a faulty deployment simply
/// re-activates a prior ReleaseId without recompiling source code.
/// </remarks>
public sealed class Factor05_BuildReleaseRunTests
{
    [Theory]
    [InlineData("20261025.1200-a1b2c3d", true)]
    [InlineData("20261025.1230-0123456789abcdef0123456789abcdef01234567", true)]
    [InlineData("v1.4.0-9f8e7d6", true)]
    [InlineData("v2.1.0-rc.1-abcdef1", true)]
    [InlineData("dev-unreleased", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("   ", false)]
    [InlineData("invalid release id with spaces", false)]
    [InlineData(" 20261025.1200-a1b2c3d ", false)]
    [InlineData("20261025.1200-a1b2c3", false)]
    [InlineData("20261025.1200-0123456789abcdef0123456789abcdef012345678", false)]
    [InlineData("20261025.1200-zzzzzzz", false)]
    [InlineData("1.4.0-9f8e7d6", false)]
    [InlineData("v1.4.0", false)]
    public void ReleaseDescriptor_ValidatesImmutableReleaseIdentifierFormat(string? releaseId, bool expectedValid)
    {
        Assert.Equal(expectedValid, ReleaseDescriptor.IsValidReleaseIdFormat(releaseId));
    }

    [Fact]
    public void ReleaseMetadataProvider_CombinesBuildArtifactAndEnvironmentConfigIntoReleaseDescriptor()
    {
        var codebase = new CodebaseInfoProvider("Catalog.Api", typeof(Factor05_BuildReleaseRunTests).Assembly, "Production");
        var options = Options.Create(new BookstoreConfigOptions { EnvironmentTier = "staging" });
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["BOOKSTORE_RELEASE_ID"] = "20261025.1230-c547602" })
            .Build();

        var provider = new ReleaseMetadataProvider(codebase, options, config);
        var descriptor = provider.GetReleaseDescriptor();

        Assert.Equal("20261025.1230-c547602", descriptor.ReleaseId);
        Assert.Equal("Catalog.Api", descriptor.ServiceName);
        Assert.Equal("staging", descriptor.EnvironmentTier);
        Assert.False(string.IsNullOrWhiteSpace(descriptor.BuildVersion));
        Assert.False(string.IsNullOrWhiteSpace(descriptor.GitCommitSha));
        Assert.True(descriptor.IsImmutableRelease);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ReleaseMetadataProvider_FallsBackToDevUnreleased_AndCodebaseEnvironment_WhenConfigUnset(string? configuredReleaseId)
    {
        var codebase = new CodebaseInfoProvider("Ordering.Api", typeof(Factor05_BuildReleaseRunTests).Assembly, "Staging");
        var options = Options.Create(new BookstoreConfigOptions { EnvironmentTier = "   " });
        var entries = configuredReleaseId is null
            ? new Dictionary<string, string?>()
            : new Dictionary<string, string?> { [ReleaseDescriptor.ReleaseIdConfigKey] = configuredReleaseId };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(entries)
            .Build();

        var provider = new ReleaseMetadataProvider(codebase, options, config);
        var descriptor = provider.GetReleaseDescriptor();

        Assert.Equal(ReleaseDescriptor.UnreleasedFallbackId, descriptor.ReleaseId);
        Assert.Equal("dev-unreleased", descriptor.ReleaseId);
        Assert.Equal("Ordering.Api", descriptor.ServiceName);
        Assert.Equal("Staging", descriptor.EnvironmentTier);
        Assert.False(descriptor.IsImmutableRelease);
    }

    [Fact]
    public void ReleaseMetadataProvider_MarksMalformedReleaseIdAsNonImmutable()
    {
        var codebase = new CodebaseInfoProvider("Fulfillment.Worker", typeof(Factor05_BuildReleaseRunTests).Assembly, "Production");
        var options = Options.Create(new BookstoreConfigOptions { EnvironmentTier = "production" });
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [ReleaseDescriptor.ReleaseIdConfigKey] = "adhoc-manual-patch" })
            .Build();

        var provider = new ReleaseMetadataProvider(codebase, options, config);
        var descriptor = provider.GetReleaseDescriptor();

        Assert.Equal("adhoc-manual-patch", descriptor.ReleaseId);
        Assert.Equal("production", descriptor.EnvironmentTier);
        Assert.False(descriptor.IsImmutableRelease);
    }

    [Fact]
    public void ReleaseMetadataProvider_ValidatesConstructorArguments()
    {
        var codebase = new CodebaseInfoProvider("Catalog.Api", typeof(Factor05_BuildReleaseRunTests).Assembly, "Production");
        var options = Options.Create(new BookstoreConfigOptions { EnvironmentTier = "production" });
        var config = new ConfigurationBuilder().Build();

        Assert.Throws<ArgumentNullException>(() => new ReleaseMetadataProvider(null!, options, config));
        Assert.Throws<ArgumentNullException>(() => new ReleaseMetadataProvider(codebase, null!, config));
        Assert.Throws<ArgumentNullException>(() => new ReleaseMetadataProvider(codebase, options, null!));
    }

    [Fact]
    public async Task ReleaseEndpointExtensions_RegistersSingletonProvider_AndMapsGetReleaseEndpoint()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [ReleaseDescriptor.ReleaseIdConfigKey] = "v1.4.0-9f8e7d6",
            ["Bookstore:EnvironmentTier"] = "production"
        });

        builder.Services.AddCodebaseMetadata("Catalog.Api", typeof(Factor05_BuildReleaseRunTests).Assembly);
        builder.Services.Configure<BookstoreConfigOptions>(
            builder.Configuration.GetSection(BookstoreConfigOptions.SectionName));
        builder.Services.AddReleaseMetadata();

        await using var app = builder.Build();
        var mapped = app.MapReleaseEndpoint();
        Assert.Same(app, mapped);

        var firstProvider = app.Services.GetRequiredService<IReleaseMetadataProvider>();
        var secondProvider = app.Services.GetRequiredService<IReleaseMetadataProvider>();
        Assert.Same(firstProvider, secondProvider);

        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/api/release");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var descriptor = await response.Content.ReadFromJsonAsync<ReleaseDescriptor>();
        Assert.NotNull(descriptor);
        Assert.Equal("v1.4.0-9f8e7d6", descriptor.ReleaseId);
        Assert.Equal("Catalog.Api", descriptor.ServiceName);
        Assert.Equal("production", descriptor.EnvironmentTier);
        Assert.True(descriptor.IsImmutableRelease);
    }

    [Fact]
    public void ServiceDefaults_GeneratesXmlDocumentationWithFactor05Remarks()
    {
        var xmlPath = Path.Combine(AppContext.BaseDirectory, "Bookstore.ServiceDefaults.xml");
        Assert.True(File.Exists(xmlPath), "XML documentation file for Bookstore.ServiceDefaults must be emitted on build.");

        var xmlContent = File.ReadAllText(xmlPath);
        Assert.Contains("12-Factor Principle: Factor V — Build, Release, Run", xmlContent);
        Assert.Contains("Release = Build(Commit SHA) + Config(Environment)", xmlContent);
        Assert.Contains("ReleaseDescriptor", xmlContent);
    }
}
