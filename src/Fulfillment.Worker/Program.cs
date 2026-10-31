using Bookstore.ServiceDefaults;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddCodebaseMetadata("Fulfillment.Worker", typeof(Fulfillment.Worker.Program).Assembly);

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
    /// Anti-patterns prevented:
    /// - Maintaining separate repository branches or repositories per deployment tier.
    /// - Hardcoding environment identities or building distinct binaries for staging versus production.
    /// - Deploying untracked binaries whose provenance cannot be mapped back to a specific commit.
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
