using Bookstore.ServiceDefaults;
using Bookstore.ServiceDefaults.BackingServices;
using Bookstore.ServiceDefaults.Configuration;
using Catalog.Api.Data;
using Catalog.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCodebaseMetadata("Catalog.Api", typeof(Catalog.Api.Program).Assembly);
builder.Services.AddBookstoreEnvironmentConfig(builder.Configuration);
builder.Services.AddAttachedResourceProbes();
builder.Services.AddDbContext<CatalogDbContext>((sp, options) =>
{
    var config = sp.GetRequiredService<IOptions<BookstoreConfigOptions>>().Value;
    options.UseNpgsql(config.Postgres.ConnectionString);
});
builder.Services.AddScoped<ICatalogRepository, PostgresCatalogRepository>();

var app = builder.Build();

app.MapCodebaseEndpoint();
app.MapConfigDiagnosticsEndpoint();
app.MapAttachedResourceEndpoints();

app.Run();

namespace Catalog.Api
{
    /// <summary>
    /// Entry point for the Catalog.Api service.
    /// </summary>
    /// <remarks>
    /// 12-Factor Principle: Factor I — Codebase
    /// One codebase tracked in revision control, many deploys.
    ///
    /// In accordance with Factor I, Catalog.Api is maintained within a single monorepo alongside all other
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
    /// Configures <see cref="CatalogDbContext"/> and <see cref="PostgresCatalogRepository"/> using the connection
    /// handle resolved from <see cref="IOptions{TOptions}"/> of <see cref="BookstoreConfigOptions"/>
    /// (<c>Bookstore__Postgres__ConnectionString</c>), alongside <see cref="BackingServiceHealthChecksExtensions.AddAttachedResourceProbes"/>
    /// and <see cref="BackingServiceHealthChecksExtensions.MapAttachedResourceEndpoints"/>. Swapping a local Docker
    /// PostgreSQL container for a managed Amazon RDS or Google Cloud SQL instance requires only an environment
    /// configuration change with zero code modifications.
    ///
    /// Anti-patterns prevented:
    /// - Maintaining separate repository branches or repositories per deployment tier.
    /// - Hardcoding environment identities or building distinct binaries for staging versus production.
    /// - Deploying untracked binaries whose provenance cannot be mapped back to a specific commit.
    /// - Scattering environment-specific <c>appsettings.{Environment}.json</c> files or credentials in source control.
    /// - Coupling persistence code to a specific database host or branching code paths between local and cloud datastores.
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
