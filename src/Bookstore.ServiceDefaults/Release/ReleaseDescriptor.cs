using System.Text.RegularExpressions;

namespace Bookstore.ServiceDefaults.Release;

/// <summary>
/// Represents the immutable Factor V release identity of a running Twelve-Factor Bookstore workload,
/// combining a compiled build artifact with a deployment environment configuration snapshot.
/// </summary>
/// <remarks>
/// 12-Factor Principle: Factor V — Build, Release, Run
/// Strictly separate build and run stages.
///
/// Factor V forbids mutating application code at runtime because changes applied directly to a running process
/// cannot propagate back to the build stage, making deployments unrepeatable and impossible to audit. Instead,
/// deployment follows a strict one-way pipeline expressed by the formula
/// <c>Release = Build(Commit SHA) + Config(Environment)</c>:
/// 1. The <b>Build</b> stage compiles a specific Git commit into an immutable executable artifact.
/// 2. The <b>Release</b> stage pairs that immutable build artifact with environment-specific configuration
///    (Factor III) and stamps the combination with a unique, immutable <see cref="ReleaseId"/>.
/// 3. The <b>Run</b> stage launches processes against that frozen release without ever recompiling or modifying code.
///
/// Assigning every release an immutable identifier (such as <c>20261025.1200-a1b2c3d</c> or <c>v1.4.0-9f8e7d6</c>)
/// creates an append-only ledger of deployments and enables instant, deterministic rollbacks simply by re-deploying
/// a prior <see cref="ReleaseId"/> without recompiling source code.
/// </remarks>
/// <param name="ReleaseId">The unique, immutable release identifier injected via <see cref="ReleaseIdConfigKey"/> (or <see cref="UnreleasedFallbackId"/> in local iterative sessions).</param>
/// <param name="ServiceName">The canonical name of the running microservice or worker workload.</param>
/// <param name="BuildVersion">The semantic or informational build version baked into the compiled assembly during the build stage.</param>
/// <param name="GitCommitSha">The source control commit SHA baked into the compiled assembly during the build stage.</param>
/// <param name="EnvironmentTier">The deployment environment tier supplied by configuration (for example, <c>staging</c> or <c>production</c>).</param>
/// <param name="IsImmutableRelease">Indicates whether <paramref name="ReleaseId"/> conforms to an immutable release identifier format rather than an ad-hoc or local fallback build.</param>
public sealed partial record ReleaseDescriptor(
    string ReleaseId,
    string ServiceName,
    string BuildVersion,
    string GitCommitSha,
    string EnvironmentTier,
    bool IsImmutableRelease)
{
    /// <summary>
    /// The fallback release identifier (<c>"dev-unreleased"</c>) reported when no explicit release identifier
    /// has been injected into the runtime environment.
    /// </summary>
    public const string UnreleasedFallbackId = "dev-unreleased";

    /// <summary>
    /// The configuration and environment variable key (<c>"BOOKSTORE_RELEASE_ID"</c>) carrying the immutable release identifier.
    /// </summary>
    public const string ReleaseIdConfigKey = "BOOKSTORE_RELEASE_ID";

    /// <summary>
    /// Validates whether the supplied string conforms to a supported immutable Factor V release identifier format:
    /// either a UTC timestamp paired with a Git commit SHA (<c>&lt;yyyyMMdd.HHmm&gt;-&lt;hexSha&gt;</c>, e.g. <c>20261025.1200-a1b2c3d</c>)
    /// or a semantic version paired with a Git commit SHA (<c>v&lt;Major&gt;.&lt;Minor&gt;.&lt;Patch&gt;[-prerelease]-&lt;hexSha&gt;</c>, e.g. <c>v1.4.0-9f8e7d6</c>),
    /// where <c>&lt;hexSha&gt;</c> is 7 to 40 hexadecimal characters.
    /// </summary>
    /// <param name="releaseId">The candidate release identifier string to validate.</param>
    /// <returns><see langword="true"/> if <paramref name="releaseId"/> matches an immutable release identifier format; otherwise, <see langword="false"/>.</returns>
    public static bool IsValidReleaseIdFormat(string? releaseId)
    {
        if (string.IsNullOrEmpty(releaseId) ||
            string.Equals(releaseId, UnreleasedFallbackId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        for (var i = 0; i < releaseId.Length; i++)
        {
            if (char.IsWhiteSpace(releaseId[i]))
            {
                return false;
            }
        }

        return TimestampShaRegex().IsMatch(releaseId) || SemanticShaRegex().IsMatch(releaseId);
    }

    [GeneratedRegex(@"\A\d{8}\.\d{4}-[0-9a-fA-F]{7,40}\z", RegexOptions.CultureInvariant)]
    private static partial Regex TimestampShaRegex();

    [GeneratedRegex(@"\Av\d+\.\d+\.\d+(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?-[0-9a-fA-F]{7,40}\z", RegexOptions.CultureInvariant)]
    private static partial Regex SemanticShaRegex();
}
