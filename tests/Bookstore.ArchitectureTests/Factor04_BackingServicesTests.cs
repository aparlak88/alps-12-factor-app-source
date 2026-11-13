using System.Net.Sockets;
using Bookstore.Contracts;
using Bookstore.ServiceDefaults.BackingServices;
using Bookstore.ServiceDefaults.Configuration;
using Catalog.Api.Data;
using Catalog.Api.Domain;
using Catalog.Api.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Xunit;

namespace Bookstore.ArchitectureTests;

/// <summary>
/// Architectural verification tests for Factor IV: Backing Services.
/// </summary>
/// <remarks>
/// 12-Factor Principle: Factor IV — Backing Services
/// Treat backing services as attached resources.
///
/// These architectural tests verify that local and third-party backing services (PostgreSQL, Redis, RabbitMQ)
/// are treated as loosely coupled attached resources bound purely via configuration handles without code changes,
/// credentials are never leaked in endpoint summaries, health checks gracefully degrade to Unhealthy when an
/// attached resource is unreachable, and docker-compose.backing.yml provisions interchangeable local containers.
/// </remarks>
public sealed class Factor04_BackingServicesTests
{
    [Fact]
    public void AttachedResourceDescriptor_SummarizesEndpointsWithoutLeakingPasswords()
    {
        var options = new BookstoreConfigOptions
        {
            EnvironmentTier = "dev",
            Postgres = new PostgresOptions { ConnectionString = "Host=pg-prod.internal;Port=5432;Database=catalog;Username=admin;Password=supersecret" },
            Redis = new RedisOptions { ConnectionString = "redis-cluster.internal:6379,password=topsecret" },
            RabbitMq = new RabbitMqOptions { Uri = "amqps://user:secretpass@rmq.cloudamqp.com:5671/vhost" }
        };

        var descriptors = BackingServiceHealthChecksExtensions.BuildDescriptorsFromOptions(options);
        Assert.Equal(3, descriptors.Count);
        Assert.All(descriptors, d =>
        {
            Assert.DoesNotContain("supersecret", d.EndpointSummary);
            Assert.DoesNotContain("topsecret", d.EndpointSummary);
            Assert.DoesNotContain("secretpass", d.EndpointSummary);
        });
        Assert.Contains(descriptors, d => d.Name == "catalog-postgres" && d.Kind == "postgresql" && d.EndpointSummary.Contains("pg-prod.internal:5432"));
        Assert.Contains(descriptors, d => d.Name == "basket-redis" && d.Kind == "redis" && d.EndpointSummary.Contains("redis-cluster.internal:6379"));
        Assert.Contains(descriptors, d => d.Name == "orders-rabbitmq" && d.Kind == "rabbitmq" && d.EndpointSummary.Contains("rmq.cloudamqp.com:5671"));
    }

    [Fact]
    public async Task AttachedResourceHealthCheck_ReturnsUnhealthyWithMetadata_WhenProbeFailsOrThrows()
    {
        var options = Options.Create(new BookstoreConfigOptions
        {
            EnvironmentTier = "staging",
            Postgres = new PostgresOptions { ConnectionString = "Host=pg-stg.internal;Port=5432;Database=catalog;Username=u;Password=p" },
            Redis = new RedisOptions { ConnectionString = "redis-stg.internal:6379" },
            RabbitMq = new RabbitMqOptions { Uri = "amqp://guest:guest@rmq-stg.internal:5672/" }
        });

        var healthyProbe = new StubAttachedResourceProbe(
            new AttachedResourceDescriptor("catalog-postgres", "postgresql", "pg-stg.internal:5432/catalog"),
            _ => Task.FromResult(true));

        var healthyCheck = new AttachedResourceHealthCheck([healthyProbe], options);
        var healthyResult = await healthyCheck.CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(HealthStatus.Healthy, healthyResult.Status);
        Assert.True(healthyResult.Data.ContainsKey("catalog-postgres"));

        var falseProbe = new StubAttachedResourceProbe(
            new AttachedResourceDescriptor("basket-redis", "redis", "redis-stg.internal:6379"),
            _ => Task.FromResult(false));

        var falseCheck = new AttachedResourceHealthCheck([healthyProbe, falseProbe], options);
        var falseResult = await falseCheck.CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(HealthStatus.Unhealthy, falseResult.Status);
        Assert.True(falseResult.Data.ContainsKey("basket-redis"));

        var throwingProbe = new StubAttachedResourceProbe(
            new AttachedResourceDescriptor("orders-rabbitmq", "rabbitmq", "rmq-stg.internal:5672/"),
            _ => throw new SocketException((int)SocketError.ConnectionRefused));

        var throwingCheck = new AttachedResourceHealthCheck([healthyProbe, throwingProbe], options);
        var throwingResult = await throwingCheck.CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(HealthStatus.Unhealthy, throwingResult.Status);
        Assert.True(throwingResult.Data.ContainsKey("orders-rabbitmq"));
    }

    [Fact]
    public async Task AddAttachedResourceProbes_AndMapAttachedResourceEndpoints_RegisterHealthChecksAndExposeDescriptors()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Bookstore:EnvironmentTier"] = "production",
            ["Bookstore:Postgres:ConnectionString"] = "Host=rds.amazonaws.com;Port=5432;Database=catalog;Username=admin;Password=rds-secret",
            ["Bookstore:Redis:ConnectionString"] = "elasticache.amazonaws.com:6379,password=redis-secret",
            ["Bookstore:RabbitMq:Uri"] = "amqps://app:rmq-secret@puffin.rmq2.cloudamqp.com:5671/prod-vhost"
        });
        builder.Services.AddBookstoreEnvironmentConfig(builder.Configuration);
        builder.Services.AddAttachedResourceProbes();

        using var app = builder.Build();
        var mapped = app.MapAttachedResourceEndpoints();
        Assert.Same(app, mapped);

        var options = app.Services.GetRequiredService<IOptions<BookstoreConfigOptions>>();
        var endpointResponse = BackingServiceHealthChecksExtensions.GetAttachedResources(options);
        var okResult = Assert.IsType<Ok<IReadOnlyList<AttachedResourceDescriptor>>>(endpointResponse);
        Assert.NotNull(okResult.Value);
        Assert.Equal(3, okResult.Value.Count);
        Assert.Contains(okResult.Value, d => d.Name == "catalog-postgres" && d.EndpointSummary == "rds.amazonaws.com:5432/catalog");
        Assert.Contains(okResult.Value, d => d.Name == "basket-redis" && d.EndpointSummary == "elasticache.amazonaws.com:6379");
        Assert.Contains(okResult.Value, d => d.Name == "orders-rabbitmq" && d.EndpointSummary == "puffin.rmq2.cloudamqp.com:5671/prod-vhost");

        var healthCheckService = app.Services.GetRequiredService<HealthCheckService>();
        var report = await healthCheckService.CheckHealthAsync(reg => reg.Tags.Contains("ready"));
        Assert.Equal(HealthStatus.Healthy, report.Status);
        Assert.True(report.Entries.ContainsKey("attached-resources"));
    }

    [Fact]
    public void DockerComposeBackingYaml_DeclaresPostgresRedisAndRabbitMqAttachedResources()
    {
        var repoRoot = FindRepoRoot();
        var composePath = Path.Combine(repoRoot, "docker-compose.backing.yml");
        Assert.True(File.Exists(composePath), "docker-compose.backing.yml must exist at the repository root.");

        var yamlContent = File.ReadAllText(composePath);
        Assert.Contains("postgres:17-alpine", yamlContent);
        Assert.Contains("redis:7.4-alpine", yamlContent);
        Assert.Contains("rabbitmq:4.0-management-alpine", yamlContent);
        Assert.Contains("pg_isready", yamlContent);
        Assert.Contains("redis-cli", yamlContent);
        Assert.Contains("rabbitmq-diagnostics", yamlContent);
        Assert.Contains("Factor IV", yamlContent);
    }

    [Fact]
    public void CatalogDbContext_ConfiguresBookEntityWithUniqueIsbnIndex()
    {
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<Catalog.Api.Data.CatalogDbContext>()
            .UseNpgsql("Host=localhost;Database=catalog_test;Username=postgres;Password=postgres")
            .Options;

        using var db = new Catalog.Api.Data.CatalogDbContext(options);
        var entityType = db.Model.FindEntityType(typeof(Catalog.Api.Domain.Book));
        Assert.NotNull(entityType);

        var isbnProperty = entityType!.FindProperty(nameof(Catalog.Api.Domain.Book.Isbn));
        Assert.NotNull(isbnProperty);
        var isbnIndex = entityType.GetIndexes().SingleOrDefault(i => i.Properties.Contains(isbnProperty!));
        Assert.NotNull(isbnIndex);
        Assert.True(isbnIndex!.IsUnique);
    }

    [Fact]
    public async Task Book_ToDto_MapsDomainEntityToContractRecord_AndPostgresCatalogRepository_ValidatesInputs()
    {
        var bookId = Guid.NewGuid();
        var book = new Book
        {
            Id = bookId,
            Isbn = "978-0321125217",
            Title = "Domain-Driven Design",
            Author = "Eric Evans",
            Price = 54.99m,
            StockQuantity = 12
        };

        var dto = book.ToDto();
        Assert.Equal(bookId, dto.Id);
        Assert.Equal("978-0321125217", dto.Isbn);
        Assert.Equal("Domain-Driven Design", dto.Title);
        Assert.Equal("Eric Evans", dto.Author);
        Assert.Equal(54.99m, dto.Price);
        Assert.Equal(12, dto.StockQuantity);

        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql("Host=localhost;Database=catalog_test;Username=postgres;Password=postgres")
            .Options;
        using var dbContext = new CatalogDbContext(options);
        var repository = new PostgresCatalogRepository(dbContext);

        await Assert.ThrowsAsync<ArgumentNullException>(() => repository.UpsertAsync(null!));
        var invalidDto = dto with { Price = -5.00m };
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repository.UpsertAsync(invalidDto));
    }

    [Fact]
    public void CatalogDbContext_AndPostgresCatalogRepository_AttachViaBookstoreConfigOptions_AllowingZeroCodeSwap()
    {
        const string localConn = "Host=localhost;Port=5432;Database=bookstore_catalog;Username=postgres;Password=postgres";
        const string cloudRdsConn = "Host=catalog-db.cluster-xyz.us-east-1.rds.amazonaws.com;Port=5432;Database=bookstore_catalog;Username=catalog_app;Password=rds-secret";

        using var localProvider = BuildCatalogServiceProvider(localConn);
        using (var scope = localProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            Assert.Contains("Host=localhost", db.Database.GetConnectionString());
            var repo = scope.ServiceProvider.GetRequiredService<ICatalogRepository>();
            Assert.IsType<PostgresCatalogRepository>(repo);
        }

        using var cloudProvider = BuildCatalogServiceProvider(cloudRdsConn);
        using (var scope = cloudProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            Assert.Contains("catalog-db.cluster-xyz.us-east-1.rds.amazonaws.com", db.Database.GetConnectionString());
            var repo = scope.ServiceProvider.GetRequiredService<ICatalogRepository>();
            Assert.IsType<PostgresCatalogRepository>(repo);
        }
    }

    private static ServiceProvider BuildCatalogServiceProvider(string postgresConnectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Bookstore:EnvironmentTier"] = "production",
                ["Bookstore:Postgres:ConnectionString"] = postgresConnectionString,
                ["Bookstore:Redis:ConnectionString"] = "localhost:6379",
                ["Bookstore:RabbitMq:Uri"] = "amqp://guest:guest@localhost:5672/"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddBookstoreEnvironmentConfig(configuration);
        services.AddDbContext<CatalogDbContext>((sp, options) =>
        {
            var config = sp.GetRequiredService<IOptions<BookstoreConfigOptions>>().Value;
            options.UseNpgsql(config.Postgres.ConnectionString);
        });
        services.AddScoped<ICatalogRepository, PostgresCatalogRepository>();
        return services.BuildServiceProvider();
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

    private sealed class StubAttachedResourceProbe(
        AttachedResourceDescriptor descriptor,
        Func<CancellationToken, Task<bool>> probeFunc) : IAttachedResourceProbe
    {
        public AttachedResourceDescriptor Descriptor { get; } = descriptor;

        public Task<bool> CheckConnectivityAsync(CancellationToken cancellationToken = default) =>
            probeFunc(cancellationToken);
    }
}
