using Bookstore.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCodebaseMetadata("Catalog.Api", typeof(Program).Assembly);

var app = builder.Build();

app.MapCodebaseEndpoint();

app.Run();

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
/// Anti-patterns prevented:
/// - Maintaining separate repository branches or repositories per deployment tier.
/// - Hardcoding environment identities or building distinct binaries for staging versus production.
/// - Deploying untracked binaries whose provenance cannot be mapped back to a specific commit.
/// </remarks>
public partial class Program
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Program"/> class.
    /// </summary>
    protected Program() { }
}
