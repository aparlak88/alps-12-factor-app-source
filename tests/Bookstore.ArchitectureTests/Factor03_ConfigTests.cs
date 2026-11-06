using Bookstore.ServiceDefaults.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Bookstore.ArchitectureTests;

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
    [InlineData("dev", "Host=pg;Database=db;Username=u;Password=p", "", "amqp://guest:guest@rmq:5672/", 50, "Redis.ConnectionString")]
    [InlineData("dev", "Host=pg;Database=db;Username=u;Password=p", "redis:6379", "http://not-amqp:5672", 50, "RabbitMq.Uri")]
    [InlineData("dev", "Host=pg;Database=db;Username=u;Password=p", "redis:6379", "amqp://guest:guest@rmq:5672/", 0, "Postgres.MaxPoolSize")]
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
}
