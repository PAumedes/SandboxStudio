namespace SandboxStudio.Core.Updates;

/// <summary>
/// Outcome of an update check.
/// </summary>
public enum UpdateCheckStatus
{
    /// <summary>The running version matches or exceeds the latest release.</summary>
    UpToDate,

    /// <summary>A newer release exists; see <see cref="UpdateCheckResult.LatestVersion"/>.</summary>
    Available,

    /// <summary>The check could not complete (network failure, malformed response, no parseable tag).</summary>
    Unavailable
}

/// <summary>
/// Result of checking the release feed for a newer version.
/// </summary>
/// <param name="Status">The check outcome.</param>
/// <param name="LatestVersion">Newest released version, when <see cref="UpdateCheckStatus.Available"/>.</param>
/// <param name="ReleaseUri">Release page to open in the browser.</param>
/// <param name="ErrorMessage">English fallback error text when the check failed.</param>
/// <param name="ErrorKey">Optional <see cref="Localization.MessageKeys"/> entry for localized display.</param>
/// <param name="ErrorArgs">Optional format arguments paired with <paramref name="ErrorKey"/>.</param>
/// <param name="LatestTag">Raw release tag (e.g. <c>v0.2.0-rc1</c>) for display; carries
/// prerelease/suffix detail that <paramref name="LatestVersion"/> drops.</param>
public sealed record UpdateCheckResult(
    UpdateCheckStatus Status,
    Version? LatestVersion = null,
    Uri? ReleaseUri = null,
    string? ErrorMessage = null,
    string? ErrorKey = null,
    IReadOnlyList<object?>? ErrorArgs = null,
    string? LatestTag = null)
{
    /// <summary>Factory for an up-to-date result.</summary>
    public static UpdateCheckResult UpToDate() => new(UpdateCheckStatus.UpToDate);

    /// <summary>Factory for a new-version result.</summary>
    public static UpdateCheckResult Available(Version latestVersion, Uri releaseUri, string? latestTag = null) =>
        new(UpdateCheckStatus.Available, latestVersion, releaseUri, LatestTag: latestTag);

    /// <summary>Factory for a check that could not complete.</summary>
    public static UpdateCheckResult Unavailable(
        string errorMessage,
        string? errorKey = null,
        IReadOnlyList<object?>? errorArgs = null) =>
        new(UpdateCheckStatus.Unavailable, ErrorMessage: errorMessage, ErrorKey: errorKey, ErrorArgs: errorArgs);
}
