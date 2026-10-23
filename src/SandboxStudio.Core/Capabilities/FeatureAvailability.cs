namespace SandboxStudio.Core.Capabilities;

/// <summary>
/// Describes whether a <see cref="SandboxFeature"/> can be used on this host and why not.
/// </summary>
/// <param name="Status">Effective availability.</param>
/// <param name="Reason">Human-readable explanation for non-available statuses.</param>
/// <param name="RequiredBuild">Minimum Windows build for build-gated features, when applicable.</param>
public sealed record FeatureAvailability(
    FeatureStatus Status,
    string? Reason = null,
    int? RequiredBuild = null);
