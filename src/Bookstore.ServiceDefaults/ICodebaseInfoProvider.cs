namespace Bookstore.ServiceDefaults;

using Bookstore.Contracts;

/// <summary>
/// Provides access to codebase revision and deployment identity metadata for the running workload.
/// </summary>
/// <remarks>
/// 12-Factor Principle: Factor I — Codebase
/// One codebase tracked in revision control, many deploys.
/// 
/// This interface satisfies Factor I by establishing a uniform contract for workloads to expose
/// their single-codebase revision identity (via Git commit SHA embedded at build time) and deployment
/// environment (e.g., Development, Staging, Production). This prevents anti-patterns such as code forks
/// per environment or untracked binary deployments where running workloads cannot be mapped to their
/// exact source repository revision.
/// </remarks>
public interface ICodebaseInfoProvider
{
    /// <summary>
    /// Gets the codebase metadata for the current running application instance.
    /// </summary>
    /// <returns>A <see cref="CodebaseMetadata"/> instance capturing service name, commit SHA, build version, and environment.</returns>
    CodebaseMetadata GetMetadata();
}
