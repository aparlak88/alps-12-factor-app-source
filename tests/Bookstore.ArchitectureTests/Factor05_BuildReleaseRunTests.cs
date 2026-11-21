using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Bookstore.ServiceDefaults;
using Bookstore.ServiceDefaults.Configuration;
using Bookstore.ServiceDefaults.Release;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Testing;
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

    [Fact]
    public void AllDockerfiles_UseMultiStageBuildWithSdkFreeRuntimeImage()
    {
        var repoRoot = FindRepoRoot();
        var dockerfiles = new[]
        {
            Path.Combine(repoRoot, "src", "Catalog.Api", "Dockerfile"),
            Path.Combine(repoRoot, "src", "Ordering.Api", "Dockerfile"),
            Path.Combine(repoRoot, "src", "Fulfillment.Worker", "Dockerfile")
        };

        foreach (var path in dockerfiles)
        {
            Assert.True(File.Exists(path), $"Dockerfile missing at {path}");
            var content = File.ReadAllText(path);
            Assert.Contains("FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build", content);
            Assert.Contains("FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime", content);
            Assert.Contains("dotnet publish", content);

            var finalStage = content.Split("AS runtime").Last();
            Assert.DoesNotContain("dotnet build", finalStage);
            Assert.DoesNotContain("sdk:10.0", finalStage);
        }
    }

    [Fact]
    public void AllDockerfiles_EnforceLockedModeRestore_SourceRevisionId_AndNonRootAppUid()
    {
        var repoRoot = FindRepoRoot();
        var expectations = new (string RelativeDir, string EntryPointDll)[]
        {
            ("Catalog.Api", "Catalog.Api.dll"),
            ("Ordering.Api", "Ordering.Api.dll"),
            ("Fulfillment.Worker", "Fulfillment.Worker.dll")
        };

        foreach (var (relativeDir, entryPointDll) in expectations)
        {
            var path = Path.Combine(repoRoot, "src", relativeDir, "Dockerfile");
            Assert.True(File.Exists(path), $"Dockerfile missing at {path}");
            var content = File.ReadAllText(path);

            Assert.Contains("12-Factor Principle: Factor V", content);
            Assert.Contains("FROM build AS publish", content);
            Assert.Contains("--locked-mode", content);
            Assert.Contains("ARG SOURCE_REVISION_ID", content);
            Assert.Contains("SourceRevisionId", content);

            var finalStage = content.Split("AS runtime").Last();
            Assert.Contains("USER $APP_UID", finalStage);
            Assert.Contains($"ENTRYPOINT [\"dotnet\", \"{entryPointDll}\"]", finalStage);
        }
    }

    [Fact]
    public void DockerIgnore_ExcludesLocalBuildOutputsAndSecrets()
    {
        var repoRoot = FindRepoRoot();
        var dockerIgnore = File.ReadAllText(Path.Combine(repoRoot, ".dockerignore"));
        Assert.Contains("**/bin/", dockerIgnore);
        Assert.Contains("**/obj/", dockerIgnore);
        Assert.Contains(".env", dockerIgnore);
    }

    [Fact]
    public async Task ReleaseScript_StampsImmutableReleaseId_RejectsMutableIds_AndRendersKustomizeManifest()
    {
        var repoRoot = FindRepoRoot();
        var scriptPath = Path.Combine(repoRoot, "scripts", "release.sh");
        Assert.True(File.Exists(scriptPath), $"Release script missing at {scriptPath}");

        var scriptContent = File.ReadAllText(scriptPath);
        Assert.Contains("date -u +%Y%m%d.%H%M", scriptContent);
        Assert.Contains("git rev-parse --short HEAD", scriptContent);
        Assert.Contains("BOOKSTORE_RELEASE_ID", scriptContent);
        Assert.Contains("SOURCE_REVISION_ID", scriptContent);

        if (!IsExecutableOnPath("kubectl"))
        {
            return;
        }

        var (exitCode, stdout, stderr) = await RunProcessAsync(
            "/usr/bin/env",
            ["bash", scriptPath, "--render-only"],
            repoRoot,
            environmentOverrides: new Dictionary<string, string?> { ["BOOKSTORE_RELEASE_ID"] = null });

        Assert.True(exitCode == 0, $"release.sh --render-only failed (exit {exitCode}): {stderr}");
        Assert.Contains("kind: Namespace", stdout);
        Assert.Contains("name: twelve-factor-bookstore", stdout);
        Assert.Matches(new Regex(@"name:\s+bookstore-release-metadata-[0-9a-z]{10}\b"), stdout);

        var releaseMatch = Regex.Match(stdout, @"BOOKSTORE_RELEASE_ID:\s*[""']?(?<id>[^\s""']+)[""']?");
        Assert.True(releaseMatch.Success, $"Rendered manifest did not contain BOOKSTORE_RELEASE_ID:\n{stdout}");
        var stampedId = releaseMatch.Groups["id"].Value;
        Assert.True(
            ReleaseDescriptor.IsValidReleaseIdFormat(stampedId),
            $"Stamped BOOKSTORE_RELEASE_ID '{stampedId}' did not satisfy ReleaseDescriptor.IsValidReleaseIdFormat.");

        var (badExitCode, _, badStderr) = await RunProcessAsync(
            "/usr/bin/env",
            ["bash", scriptPath, "--render-only"],
            repoRoot,
            environmentOverrides: new Dictionary<string, string?> { ["BOOKSTORE_RELEASE_ID"] = "latest" });

        Assert.NotEqual(0, badExitCode);
        Assert.Contains("BOOKSTORE_RELEASE_ID", badStderr);
    }

    [Fact]
    public async Task KustomizeBase_DefinesNamespaceAndContentHashedReleaseMetadata_AndRendersCleanly()
    {
        var repoRoot = FindRepoRoot();
        var baseDir = Path.Combine(repoRoot, "deploy", "k8s", "base");
        var namespacePath = Path.Combine(baseDir, "namespace.yaml");
        var kustomizationPath = Path.Combine(baseDir, "kustomization.yaml");

        Assert.True(File.Exists(namespacePath), $"Missing {namespacePath}");
        Assert.True(File.Exists(kustomizationPath), $"Missing {kustomizationPath}");

        var namespaceYaml = File.ReadAllText(namespacePath);
        Assert.Contains("kind: Namespace", namespaceYaml);
        Assert.Contains("name: twelve-factor-bookstore", namespaceYaml);

        var kustomizationYaml = File.ReadAllText(kustomizationPath);
        Assert.Contains("namespace: twelve-factor-bookstore", kustomizationYaml);
        Assert.Contains("namespace.yaml", kustomizationYaml);
        Assert.Contains("configmap.yaml", kustomizationYaml);
        Assert.Contains("secret.yaml", kustomizationYaml);
        Assert.Contains("bookstore-release-metadata", kustomizationYaml);
        Assert.Contains("BOOKSTORE_RELEASE_ID=", kustomizationYaml);
        Assert.Contains("bookstore/catalog-api", kustomizationYaml);
        Assert.Contains("bookstore/ordering-api", kustomizationYaml);
        Assert.Contains("bookstore/fulfillment-worker", kustomizationYaml);

        if (!IsExecutableOnPath("kubectl"))
        {
            return;
        }

        var (exitCode, stdout, stderr) = await RunProcessAsync(
            "kubectl",
            ["kustomize", baseDir],
            repoRoot);

        Assert.True(exitCode == 0, $"kubectl kustomize failed (exit {exitCode}): {stderr}");
        Assert.Contains("kind: Namespace", stdout);
        Assert.Contains("name: bookstore-config", stdout);
        Assert.Contains("name: bookstore-secrets", stdout);
        Assert.Matches(new Regex(@"name:\s+bookstore-release-metadata-[0-9a-z]{10}\b"), stdout);
    }

    [Fact]
    public async Task CatalogApi_AndOrderingApi_ExposeReleaseEndpoint_AndFulfillmentWorkerWiresReleaseMetadata()
    {
        await using var catalogFactory = new WebApplicationFactory<Catalog.Api.Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["BOOKSTORE_RELEASE_ID"] = "20261025.1500-a1b2c3d",
                        ["Bookstore:EnvironmentTier"] = "staging",
                        ["Bookstore:Postgres:ConnectionString"] = "Host=pg-test.internal;Port=5432;Database=catalog;Username=u;Password=secret-pg",
                        ["Bookstore:Redis:ConnectionString"] = "redis-test.internal:6379,password=secret-redis",
                        ["Bookstore:RabbitMq:Uri"] = "amqps://u:secret-rmq@rmq-test.internal:5671/vhost"
                    });
                });
            });

        using var catalogClient = catalogFactory.CreateClient();
        var catalogRelease = await catalogClient.GetFromJsonAsync<ReleaseDescriptor>("/api/release");
        Assert.NotNull(catalogRelease);
        Assert.Equal("20261025.1500-a1b2c3d", catalogRelease!.ReleaseId);
        Assert.Equal("Catalog.Api", catalogRelease.ServiceName);
        Assert.Equal("staging", catalogRelease.EnvironmentTier);
        Assert.True(catalogRelease.IsImmutableRelease);

        await using var orderingFactory = new WebApplicationFactory<Ordering.Api.Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["BOOKSTORE_RELEASE_ID"] = "",
                        ["Bookstore:EnvironmentTier"] = "local-orbstack",
                        ["Bookstore:Postgres:ConnectionString"] = "Host=pg-test.internal;Port=5432;Database=catalog;Username=u;Password=secret-pg",
                        ["Bookstore:Redis:ConnectionString"] = "redis-test.internal:6379,password=secret-redis",
                        ["Bookstore:RabbitMq:Uri"] = "amqps://u:secret-rmq@rmq-test.internal:5671/vhost"
                    });
                });
            });

        using var orderingClient = orderingFactory.CreateClient();
        var orderingRelease = await orderingClient.GetFromJsonAsync<ReleaseDescriptor>("/api/release");
        Assert.NotNull(orderingRelease);
        Assert.Equal(ReleaseDescriptor.UnreleasedFallbackId, orderingRelease!.ReleaseId);
        Assert.Equal("Ordering.Api", orderingRelease.ServiceName);
        Assert.Equal("local-orbstack", orderingRelease.EnvironmentTier);
        Assert.False(orderingRelease.IsImmutableRelease);

        var workerProgramSource = File.ReadAllText(
            Path.Combine(FindRepoRoot(), "src", "Fulfillment.Worker", "Program.cs"));
        Assert.Contains("AddReleaseMetadata()", workerProgramSource);
    }

    [Theory]
    [InlineData("Catalog.Api.xml")]
    [InlineData("Ordering.Api.xml")]
    [InlineData("Fulfillment.Worker.xml")]
    public void Workloads_GenerateXmlDocumentationWithFactor05Remarks(string xmlFileName)
    {
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFileName);
        Assert.True(File.Exists(xmlPath), $"XML documentation file '{xmlFileName}' must be emitted on build.");
        var xmlContent = File.ReadAllText(xmlPath);
        Assert.Contains("12-Factor Principle: Factor V — Build, Release, Run", xmlContent);
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

    private static bool IsExecutableOnPath(string command)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathEnv))
        {
            return false;
        }

        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir, command);
            if (File.Exists(candidate))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunProcessAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string?>? environmentOverrides = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var arg in arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        if (environmentOverrides is not null)
        {
            foreach (var (key, value) in environmentOverrides)
            {
                if (value is null)
                {
                    startInfo.Environment.Remove(key);
                }
                else
                {
                    startInfo.Environment[key] = value;
                }
            }
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start process '{fileName}'.");

        var stdOutTask = process.StandardOutput.ReadToEndAsync();
        var stdErrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (process.ExitCode, await stdOutTask, await stdErrTask);
    }
}
