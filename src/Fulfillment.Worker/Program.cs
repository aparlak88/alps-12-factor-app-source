using Bookstore.ServiceDefaults;
using Bookstore.ServiceDefaults.BackingServices;
using Bookstore.ServiceDefaults.Configuration;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddCodebaseMetadata("Fulfillment.Worker", typeof(Fulfillment.Worker.Program).Assembly);
builder.Services.AddBookstoreEnvironmentConfig(builder.Configuration);
builder.Services.AddAttachedResourceProbes();

var host = builder.Build();
host.Run();

namespace Fulfillment.Worker
{
    /// <summary>
    /// Entry point for the Fulfillment.Worker service.
    /// </summary>
    /// <remarks>
    /// 12-Factor Principle: Factor I — Codebase
    /// One codebase tracked in revision control, many deploys.
    ///
    /// In accordance with Factor I, Fulfillment.Worker is maintained within a single monorepo alongside all other
    /// Bookstore services and shared libraries. The worker artifact embeds its build-time Git commit SHA
    /// (via SourceRevisionId) while dynamically distinguishing runtime deployments (Development, Staging,
    /// Production) via environment configuration.
    ///
    /// 12-Factor Principle: Factor III — Config
    /// Store config in the environment.
    ///
    /// Passes the Factor III open-source litmus test ("could this codebase be open-sourced right now without
    /// compromising any credentials or environment URLs?") by binding <see cref="BookstoreConfigOptions"/>
    /// strictly from environment variables via <see cref="ConfigServiceCollectionExtensions.AddBookstoreEnvironmentConfig"/>
    /// with fail-fast startup validation (<c>ValidateOnStart()</c>).
    ///
    /// 12-Factor Principle: Factor IV — Backing Services
    /// Treat backing services as attached resources.
    ///
    /// Registers <see cref="BackingServiceHealthChecksExtensions.AddAttachedResourceProbes"/> so the worker's
    /// attached message broker and backing resource handles are monitored via <see cref="BookstoreConfigOptions"/>
    /// without coupling the worker binary to a specific RabbitMQ host or environment topology.
    ///
    /// Anti-patterns prevented:
    /// - Maintaining separate repository branches or repositories per deployment tier.
    /// - Hardcoding environment identities or building distinct binaries for staging versus production.
    /// - Deploying untracked binaries whose provenance cannot be mapped back to a specific commit.
    /// - Scattering environment-specific <c>appsettings.{Environment}.json</c> files or credentials in source control.
    /// - Coupling worker consumers to a fixed local or cloud message broker address in source code.
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
