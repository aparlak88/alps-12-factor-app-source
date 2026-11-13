using Bookstore.ServiceDefaults;
using Bookstore.ServiceDefaults.BackingServices;
using Bookstore.ServiceDefaults.Configuration;
using Ordering.Api.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCodebaseMetadata("Ordering.Api", typeof(Ordering.Api.Program).Assembly);
builder.Services.AddBookstoreEnvironmentConfig(builder.Configuration);
builder.Services.AddAttachedResourceProbes();
builder.Services.AddSingleton<IBasketStore, RedisBasketStore>();
builder.Services.AddSingleton<IOrderEventPublisher, RabbitMqOrderEventPublisher>();

var app = builder.Build();

app.MapCodebaseEndpoint();
app.MapConfigDiagnosticsEndpoint();
app.MapAttachedResourceEndpoints();

app.Run();

namespace Ordering.Api
{
    /// <summary>
    /// Entry point for the Ordering.Api service.
    /// </summary>
    /// <remarks>
    /// 12-Factor Principle: Factor I — Codebase
    /// One codebase tracked in revision control, many deploys.
    ///
    /// In accordance with Factor I, Ordering.Api is maintained within a single monorepo alongside all other
    /// Bookstore services and shared libraries. The service artifact embeds its build-time Git commit SHA
    /// (via SourceRevisionId) while dynamically distinguishing runtime deployments (Development, Staging,
    /// Production) via environment configuration.
    ///
    /// 12-Factor Principle: Factor III — Config
    /// Store config in the environment.
    ///
    /// Passes the Factor III open-source litmus test ("could this codebase be open-sourced right now without
    /// compromising any credentials or environment URLs?") by binding <see cref="BookstoreConfigOptions"/>
    /// strictly from environment variables via <see cref="ConfigServiceCollectionExtensions.AddBookstoreEnvironmentConfig"/>
    /// with fail-fast startup validation (<c>ValidateOnStart()</c>) and exposing redacted configuration diagnostics
    /// at <c>GET /api/config/diagnostics</c>.
    ///
    /// 12-Factor Principle: Factor IV — Backing Services
    /// Treat backing services as attached resources.
    ///
    /// Registers <see cref="RedisBasketStore"/> (<see cref="IBasketStore"/>) and <see cref="RabbitMqOrderEventPublisher"/>
    /// (<see cref="IOrderEventPublisher"/>) bound exclusively to environment-injected <see cref="BookstoreConfigOptions"/>
    /// handles (<c>Bookstore__Redis__ConnectionString</c> and <c>Bookstore__RabbitMq__Uri</c>), alongside
    /// <see cref="BackingServiceHealthChecksExtensions.AddAttachedResourceProbes"/> and
    /// <see cref="BackingServiceHealthChecksExtensions.MapAttachedResourceEndpoints"/>. Local Docker containers can be
    /// swapped for managed Amazon ElastiCache or CloudAMQP instances purely via environment configuration without code changes.
    ///
    /// Anti-patterns prevented:
    /// - Maintaining separate repository branches or repositories per deployment tier.
    /// - Hardcoding environment identities or building distinct binaries for staging versus production.
    /// - Deploying untracked binaries whose provenance cannot be mapped back to a specific commit.
    /// - Scattering environment-specific <c>appsettings.{Environment}.json</c> files or credentials in source control.
    /// - Hardcoding Redis or RabbitMQ hostnames in code or branching service implementations between local and cloud brokers.
    /// </remarks>
    public partial class Program
    {
        /// <summary>
        /// Gets the shared contracts marker type to ensure the <c>Bookstore.Contracts</c> assembly reference is preserved in the compiled manifest.
        /// </summary>
        public static Type ContractsAssemblyMarker => typeof(Bookstore.Contracts.CodebaseMetadata);

        /// <summary>
        /// Initializes a new instance of the <see cref="Program"/> class.
        /// </summary>
        protected Program() { }
    }
}
