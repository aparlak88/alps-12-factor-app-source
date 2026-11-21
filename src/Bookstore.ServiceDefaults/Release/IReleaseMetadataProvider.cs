namespace Bookstore.ServiceDefaults.Release;

/// <summary>
/// Provides access to the Factor V immutable release descriptor for the currently executing workload.
/// </summary>
/// <remarks>
/// 12-Factor Principle: Factor V — Build, Release, Run
/// Strictly separate build and run stages.
///
/// Under Factor V, application code can never be mutated at runtime because runtime code edits bypass the build
/// stage and break deployment reproducibility. Every deployment instead adheres to the composition formula
/// <c>Release = Build(Commit SHA) + Config(Environment)</c>, combining a single compiled build artifact with an
/// environment configuration snapshot under an immutable release identifier. Exposing that identity via
/// <see cref="IReleaseMetadataProvider"/> allows operators and automated pipelines to audit active releases and
/// execute rollbacks by re-deploying a previous immutable release ID without recompiling.
/// </remarks>
public interface IReleaseMetadataProvider
{
    /// <summary>
    /// Resolves the <see cref="ReleaseDescriptor"/> representing the running build artifact and environment configuration.
    /// </summary>
    /// <returns>A <see cref="ReleaseDescriptor"/> capturing the release ID, build metadata, environment tier, and immutability flag.</returns>
    ReleaseDescriptor GetReleaseDescriptor();
}
