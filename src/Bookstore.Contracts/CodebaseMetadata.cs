namespace Bookstore.Contracts;

/// <summary>
/// Captures build, revision, and runtime environment metadata for a deployed service workload.
/// </summary>
/// <param name="ServiceName">The canonical name of the running service or workload.</param>
/// <param name="GitCommitSha">The source control commit SHA from which the binary was built.</param>
/// <param name="BuildVersion">The build or informational version of the compiled package.</param>
/// <param name="EnvironmentName">The deployment environment name (e.g., Development, Staging, Production).</param>
/// <remarks>
/// 12-Factor Principle: Factor I — Codebase
/// One codebase tracked in revision control, many deploys.
/// 
/// Factor I mandates one codebase tracked in revision control, with many deploys across distinct environments.
/// <see cref="CodebaseMetadata"/> allows running workloads to report their exact Git commit SHA and build version,
/// enabling continuous verification that distinct deployments (staging, production) map back to specific revisions
/// in the single shared repository.
/// </remarks>
public sealed record CodebaseMetadata(string ServiceName, string GitCommitSha, string BuildVersion, string EnvironmentName);
