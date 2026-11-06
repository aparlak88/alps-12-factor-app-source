using Bookstore.ServiceDefaults.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Bookstore.ArchitectureTests;

/// <summary>
/// Architectural verification tests for Factor III: Config.
/// </summary>
/// <remarks>
/// 12-Factor Principle: Factor III — Config
/// Store config in the environment.
///
/// These architectural tests verify that BookstoreConfigOptions binds from hierarchical environment variables,
/// BookstoreOptionsValidator fails fast on missing or invalid settings (including hardcoded secret placeholders),
/// zero environment-specific appsettings.*.json files exist in the repository, Kubernetes manifests separate
/// non-sensitive ConfigMap knobs from Secret credentials, and /api/config/diagnostics redacts all connection strings.
/// </remarks>
public sealed class Factor03_ConfigTests
{
    [Fact]
    public void BookstoreConfigOptions_BindsFromHierarchicalEnvironmentVariables_AndPassesValidation()
    {
        var envVars = new Dictionary<string, string?>
        {
            ["Bookstore:EnvironmentTier"] = "local-orbstack",
            ["Bookstore:Postgres:ConnectionString"] = "Host=postgres;Port=5432;Database=catalog;Username=bookstore;Password=secret",
            ["Bookstore:Postgres:MaxPoolSize"] = "100",
            ["Bookstore:Redis:ConnectionString"] = "redis:6379",
            ["Bookstore:RabbitMq:Uri"] = "amqp://guest:guest@rabbitmq:5672/"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(envVars)
            .Build();

        var services = new ServiceCollection();
        services.AddBookstoreEnvironmentConfig(configuration);
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<BookstoreConfigOptions>>().Value;
        Assert.Equal("local-orbstack", options.EnvironmentTier);
        Assert.Equal(100, options.Postgres.MaxPoolSize);
        Assert.Equal("redis:6379", options.Redis.ConnectionString);
    }

    [Theory]
    [InlineData("", "Host=pg;Database=db;Username=u;Password=p", "redis:6379", "amqp://guest:guest@rmq:5672/", 50, "EnvironmentTier")]
    [InlineData("dev", "", "redis:6379", "amqp://guest:guest@rmq:5672/", 50, "Postgres.ConnectionString")]
    [InlineData("dev", "Host=pg;Password=CONTAINS_HARDCODED_SECRET", "redis:6379", "amqp://guest:guest@rmq:5672/", 50, "Postgres.ConnectionString")]
    [InlineData("dev", "Host=pg;Database=db;Username=u;Password=p", "", "amqp://guest:guest@rmq:5672/", 50, "Redis.ConnectionString")]
    [InlineData("dev", "Host=pg;Database=db;Username=u;Password=p", "redis:6379,password=CONTAINS_HARDCODED_SECRET", "amqp://guest:guest@rmq:5672/", 50, "Redis.ConnectionString")]
    [InlineData("dev", "Host=pg;Database=db;Username=u;Password=p", "redis:6379", "http://not-amqp:5672", 50, "RabbitMq.Uri")]
    [InlineData("dev", "Host=pg;Database=db;Username=u;Password=p", "redis:6379", "amqp://guest:CONTAINS_HARDCODED_SECRET@rmq:5672/", 50, "RabbitMq.Uri")]
    [InlineData("dev", "Host=pg;Database=db;Username=u;Password=p", "redis:6379", "amqp://guest:guest@rmq:5672/", 0, "Postgres.MaxPoolSize")]
    [InlineData("dev", "Host=pg;Database=db;Username=u;Password=p", "redis:6379", "amqp://guest:guest@rmq:5672/", 501, "Postgres.MaxPoolSize")]
    public void BookstoreOptionsValidator_FailsFast_WhenRequiredEnvironmentVariablesAreInvalid(
        string tier,
        string pgConn,
        string redisConn,
        string rmqUri,
        int poolSize,
        string expectedFailureField)
    {
        var validator = new BookstoreOptionsValidator();
        var options = new BookstoreConfigOptions
        {
            EnvironmentTier = tier,
            Postgres = new PostgresOptions { ConnectionString = pgConn, MaxPoolSize = poolSize },
            Redis = new RedisOptions { ConnectionString = redisConn },
            RabbitMq = new RabbitMqOptions { Uri = rmqUri }
        };

        var result = validator.Validate(Options.DefaultName, options);
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains(expectedFailureField, StringComparison.Ordinal));
    }

    [Fact]
    public void Repository_ContainsNoEnvironmentSpecificAppsettingsFiles()
    {
        var repoRoot = FindRepoRoot();
        var envAppsettings = Directory.GetFiles(repoRoot, "appsettings.*.json", SearchOption.AllDirectories);
        Assert.Empty(envAppsettings);
    }

    [Fact]
    public void KubernetesManifests_SeparateConfigMapFromSecrets()
    {
        var repoRoot = FindRepoRoot();
        var configMapYaml = File.ReadAllText(Path.Combine(repoRoot, "deploy", "k8s", "base", "configmap.yaml"));
        var secretYaml = File.ReadAllText(Path.Combine(repoRoot, "deploy", "k8s", "base", "secret.yaml"));

        Assert.Contains("kind: ConfigMap", configMapYaml);
        Assert.Contains("Bookstore__EnvironmentTier", configMapYaml);
        Assert.DoesNotContain("ConnectionString", configMapYaml);

        Assert.Contains("kind: Secret", secretYaml);
        Assert.Contains("Bookstore__Postgres__ConnectionString", secretYaml);
        Assert.Contains("Bookstore__Redis__ConnectionString", secretYaml);
        Assert.Contains("Bookstore__RabbitMq__Uri", secretYaml);
    }

    [Fact]
    public void AddBookstoreEnvironmentConfig_AppliesFallbackDefaults_OnlyWhenExplicitlyAllowed()
    {
        var emptyConfig = new ConfigurationBuilder().Build();
        var strictServices = new ServiceCollection();
        strictServices.AddBookstoreEnvironmentConfig(emptyConfig);
        using var strictProvider = strictServices.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() =>
            _ = strictProvider.GetRequiredService<IOptions<BookstoreConfigOptions>>().Value);

        var fallbackConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [ConfigServiceCollectionExtensions.AllowFallbackDefaultsKey] = "true",
                ["Bookstore:Postgres:MaxPoolSize"] = "75"
            })
            .Build();

        var fallbackServices = new ServiceCollection();
        fallbackServices.AddBookstoreEnvironmentConfig(fallbackConfig);
        using var fallbackProvider = fallbackServices.BuildServiceProvider();

        var resolved = fallbackProvider.GetRequiredService<IOptions<BookstoreConfigOptions>>().Value;
        Assert.Equal("local-fallback", resolved.EnvironmentTier);
        Assert.Equal(75, resolved.Postgres.MaxPoolSize);
        Assert.False(string.IsNullOrWhiteSpace(resolved.Postgres.ConnectionString));
        Assert.False(string.IsNullOrWhiteSpace(resolved.Redis.ConnectionString));
        Assert.StartsWith("amqp://", resolved.RabbitMq.Uri, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RedactedConfigDiagnostics_RedactsAllConnectionStrings_AndExposesNonSensitiveKnobs()
    {
        var options = new BookstoreConfigOptions
        {
            EnvironmentTier = "production",
            Postgres = new PostgresOptions
            {
                ConnectionString = "Host=prod-db.internal;Port=5432;Database=catalog;Username=app;Password=topsecret-pg",
                MaxPoolSize = 120
            },
            Redis = new RedisOptions
            {
                ConnectionString = "prod-redis.internal:6379,password=topsecret-redis",
                InstancePrefix = "prod-bookstore:"
            },
            RabbitMq = new RabbitMqOptions
            {
                Uri = "amqps://prod-user:topsecret-rmq@prod-rmq.internal:5671/vhost",
                OrderQueueName = "prod.orders.placed"
            }
        };

        var diagnostics = RedactedConfigDiagnostics.FromOptions(options);

        Assert.Equal("production", diagnostics.EnvironmentTier);
        Assert.Equal("***REDACTED***", diagnostics.Postgres.ConnectionString);
        Assert.Equal(120, diagnostics.Postgres.MaxPoolSize);
        Assert.Equal("***REDACTED***", diagnostics.Redis.ConnectionString);
        Assert.Equal("prod-bookstore:", diagnostics.Redis.InstancePrefix);
        Assert.Equal("***REDACTED***", diagnostics.RabbitMq.Uri);
        Assert.Equal("prod.orders.placed", diagnostics.RabbitMq.OrderQueueName);

        var builder = WebApplication.CreateSlimBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Bookstore:EnvironmentTier"] = "staging",
            ["Bookstore:Postgres:ConnectionString"] = "Host=stg-db;Database=catalog;Username=u;Password=secret-pg",
            ["Bookstore:Postgres:MaxPoolSize"] = "80",
            ["Bookstore:Redis:ConnectionString"] = "stg-redis:6379,password=secret-redis",
            ["Bookstore:Redis:InstancePrefix"] = "stg:",
            ["Bookstore:RabbitMq:Uri"] = "amqp://guest:secret-rmq@stg-rmq:5672/",
            ["Bookstore:RabbitMq:OrderQueueName"] = "stg.orders"
        });
        builder.Services.AddBookstoreEnvironmentConfig(builder.Configuration);

        using var app = builder.Build();
        var mapped = app.MapConfigDiagnosticsEndpoint();
        Assert.Same(app, mapped);

        var resolvedOptions = app.Services.GetRequiredService<IOptions<BookstoreConfigOptions>>();
        var endpointResult = ConfigServiceCollectionExtensions.GetConfigDiagnostics(resolvedOptions);
        var okResult = Assert.IsType<Ok<RedactedConfigDiagnostics>>(endpointResult);
        Assert.NotNull(okResult.Value);
        Assert.Equal("staging", okResult.Value.EnvironmentTier);
        Assert.Equal("***REDACTED***", okResult.Value.Postgres.ConnectionString);
        Assert.Equal(80, okResult.Value.Postgres.MaxPoolSize);
        Assert.Equal("***REDACTED***", okResult.Value.Redis.ConnectionString);
        Assert.Equal("stg:", okResult.Value.Redis.InstancePrefix);
        Assert.Equal("***REDACTED***", okResult.Value.RabbitMq.Uri);
        Assert.Equal("stg.orders", okResult.Value.RabbitMq.OrderQueueName);
    }

    [Theory]
    [InlineData("Bookstore.ServiceDefaults.xml")]
    [InlineData("Catalog.Api.xml")]
    [InlineData("Ordering.Api.xml")]
    [InlineData("Fulfillment.Worker.xml")]
    public void ServiceDefaults_AndWorkloads_GenerateXmlDocumentationWithFactor03Remarks(string xmlFileName)
    {
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFileName);
        Assert.True(File.Exists(xmlPath), $"XML documentation file '{xmlFileName}' must be emitted on build.");
        var xmlContent = File.ReadAllText(xmlPath);
        Assert.Contains("12-Factor Principle: Factor III — Config", xmlContent);
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
