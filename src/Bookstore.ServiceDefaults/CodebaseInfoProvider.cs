namespace Bookstore.ServiceDefaults;

using System.Reflection;
using Bookstore.Contracts;

/// <summary>
/// Extracts and supplies codebase revision and deployment identity metadata from assembly informational version attributes.
/// </summary>
/// <remarks>
/// 12-Factor Principle: Factor I — Codebase
/// One codebase tracked in revision control, many deploys.
/// 
/// Factor I dictates that a 12-factor application tracks a single codebase in revision control while allowing
/// many deploys (Development, Staging, Production) from that codebase.
/// 
/// <see cref="CodebaseInfoProvider"/> enforces this principle by:
/// 1. Extracting the build-time Git commit SHA from <see cref="AssemblyInformationalVersionAttribute"/> injected
///    via &lt;IncludeSourceRevisionInInformationalVersion&gt; during compile time, ensuring every running artifact has provenance linking back
///    to the exact monorepo commit.
/// 2. Resolving the runtime environment (Development, Staging, Production) independently from the code,
///    verifying that the identical compiled binary artifact can be deployed across different environments without code mutations.
/// 3. Providing robust fallbacks ("unknown-revision" and "0.0.0" when metadata is missing outside Git, and defaulting
///    to "Production" when no environment is explicitly specified or discovered via environment variables).
/// 
/// Anti-patterns prevented:
/// - Multiple repositories or forks for different environments.
/// - Environment-specific binary builds or branching divergence.
/// - Unversioned deployments that cannot be audited or traced back to source control.
/// </remarks>
public sealed class CodebaseInfoProvider : ICodebaseInfoProvider
{
    private const string DefaultVersion = "0.0.0";
    private const string DefaultSha = "unknown-revision";
    private const string DefaultEnvironment = "Production";

    private readonly CodebaseMetadata _metadata;

    /// <summary>
    /// Initializes a new instance of the <see cref="CodebaseInfoProvider"/> class.
    /// </summary>
    /// <param name="serviceName">The canonical name of the running service or workload.</param>
    /// <param name="entryAssembly">The entry assembly containing compile-time informational version metadata.</param>
    public CodebaseInfoProvider(string serviceName, Assembly entryAssembly)
        : this(serviceName, entryAssembly, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CodebaseInfoProvider"/> class with an explicit deployment environment.
    /// </summary>
    /// <param name="serviceName">The canonical name of the running service or workload.</param>
    /// <param name="entryAssembly">The entry assembly containing compile-time informational version metadata.</param>
    /// <param name="environmentName">The deployment environment name, or <see langword="null"/> to resolve from environment variables or default to "Production".</param>
    public CodebaseInfoProvider(string serviceName, Assembly entryAssembly, string? environmentName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        ArgumentNullException.ThrowIfNull(entryAssembly);

        var informationalVersion = entryAssembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        var (buildVersion, gitCommitSha) = ParseInformationalVersion(informationalVersion);
        var resolvedEnvironment = ResolveEnvironmentName(environmentName);

        _metadata = new CodebaseMetadata(serviceName, gitCommitSha, buildVersion, resolvedEnvironment);
    }

    /// <inheritdoc />
    public CodebaseMetadata GetMetadata() => _metadata;

    /// <summary>
    /// Parses an informational version string (such as "1.0.0+commitSha") into its semantic build version and Git revision components.
    /// </summary>
    /// <param name="informationalVersion">The raw informational version string from the assembly metadata.</param>
    /// <returns>A tuple containing the build version and Git commit SHA, falling back to "0.0.0" and "unknown-revision" when missing.</returns>
    public static (string BuildVersion, string GitCommitSha) ParseInformationalVersion(string? informationalVersion)
    {
        if (string.IsNullOrWhiteSpace(informationalVersion))
        {
            return (DefaultVersion, DefaultSha);
        }

        var plusIndex = informationalVersion.IndexOf('+');
        if (plusIndex < 0)
        {
            var trimmed = informationalVersion.Trim();
            return (trimmed.Length > 0 ? trimmed : DefaultVersion, DefaultSha);
        }

        var versionPart = informationalVersion[..plusIndex].Trim();
        var shaPart = informationalVersion[(plusIndex + 1)..].Trim();

        var buildVersion = string.IsNullOrWhiteSpace(versionPart) ? DefaultVersion : versionPart;
        var gitCommitSha = string.IsNullOrWhiteSpace(shaPart) ? DefaultSha : shaPart;

        return (buildVersion, gitCommitSha);
    }

    private static string ResolveEnvironmentName(string? environmentName)
    {
        if (!string.IsNullOrWhiteSpace(environmentName))
        {
            return environmentName;
        }

        var aspnetEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        if (!string.IsNullOrWhiteSpace(aspnetEnv))
        {
            return aspnetEnv;
        }

        var dotnetEnv = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        if (!string.IsNullOrWhiteSpace(dotnetEnv))
        {
            return dotnetEnv;
        }

        return DefaultEnvironment;
    }
}
