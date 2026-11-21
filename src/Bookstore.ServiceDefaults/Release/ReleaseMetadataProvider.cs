using Bookstore.ServiceDefaults.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Bookstore.ServiceDefaults.Release;

/// <summary>
/// Composes a Factor V <see cref="ReleaseDescriptor"/> by pairing compile-time build metadata from
/// <see cref="ICodebaseInfoProvider"/> with runtime environment configuration from <see cref="BookstoreConfigOptions"/>
/// and the <see cref="ReleaseDescriptor.ReleaseIdConfigKey"/> setting.
/// </summary>
/// <remarks>
/// 12-Factor Principle: Factor V — Build, Release, Run
/// Strictly separate build and run stages.
///
/// Runtime code mutation is strictly prohibited in a twelve-factor architecture because ad-hoc edits on live hosts
/// cannot flow backward into source control or the build stage, guaranteeing drift on the next restart or scale-out.
/// <see cref="ReleaseMetadataProvider"/> materializes the Factor V composition formula
/// <c>Release = Build(Commit SHA) + Config(Environment)</c> by uniting the immutable Factor I build provenance
/// (<c>BuildVersion</c> and <c>GitCommitSha</c>) with the Factor III environment tier and the immutable release ID
/// (<c>BOOKSTORE_RELEASE_ID</c>). Because each release carries an immutable ID, operators can roll back instantaneously
/// to a known-good release without running <c>dotnet build</c> or recompiling source code.
/// </remarks>
public sealed class ReleaseMetadataProvider : IReleaseMetadataProvider
{
    private readonly ICodebaseInfoProvider _codebaseInfo;
    private readonly IOptions<BookstoreConfigOptions> _configOptions;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReleaseMetadataProvider"/> class.
    /// </summary>
    /// <param name="codebaseInfo">The Factor I codebase metadata provider supplying the compiled build version and Git commit SHA.</param>
    /// <param name="configOptions">The Factor III strongly-typed configuration options supplying the deployment environment tier.</param>
    /// <param name="configuration">The application configuration root from which <see cref="ReleaseDescriptor.ReleaseIdConfigKey"/> is read.</param>
    public ReleaseMetadataProvider(
        ICodebaseInfoProvider codebaseInfo,
        IOptions<BookstoreConfigOptions> configOptions,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(codebaseInfo);
        ArgumentNullException.ThrowIfNull(configOptions);
        ArgumentNullException.ThrowIfNull(configuration);

        _codebaseInfo = codebaseInfo;
        _configOptions = configOptions;
        _configuration = configuration;
    }

    /// <inheritdoc />
    public ReleaseDescriptor GetReleaseDescriptor()
    {
        var codebase = _codebaseInfo.GetMetadata();
        var configuredReleaseId = _configuration[ReleaseDescriptor.ReleaseIdConfigKey];

        string releaseId;
        bool isImmutableRelease;

        if (string.IsNullOrWhiteSpace(configuredReleaseId))
        {
            releaseId = ReleaseDescriptor.UnreleasedFallbackId;
            isImmutableRelease = false;
        }
        else
        {
            releaseId = configuredReleaseId;
            isImmutableRelease = ReleaseDescriptor.IsValidReleaseIdFormat(releaseId);
        }

        var configuredTier = _configOptions.Value?.EnvironmentTier;
        var environmentTier = string.IsNullOrWhiteSpace(configuredTier)
            ? codebase.EnvironmentName
            : configuredTier;

        return new ReleaseDescriptor(
            ReleaseId: releaseId,
            ServiceName: codebase.ServiceName,
            BuildVersion: codebase.BuildVersion,
            GitCommitSha: codebase.GitCommitSha,
            EnvironmentTier: environmentTier,
            IsImmutableRelease: isImmutableRelease);
    }
}
