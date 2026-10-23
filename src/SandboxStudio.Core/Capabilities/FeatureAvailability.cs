namespace SandboxStudio.Core.Capabilities;

/// <summary>
/// Describes whether a <see cref="SandboxFeature"/> can be used on this host and why not.
/// </summary>
/// <param name="Status">Effective availability.</param>
/// <param name="Reason">Human-readable explanation for non-available statuses.</param>
/// <param name="RequiredBuild">Minimum Windows build for build-gated features, when applicable.</param>
/// <param name="ReasonKey">Optional <see cref="Localization.MessageKeys"/> entry identifying the
/// reason template so hosts can localize it; <paramref name="Reason"/> remains the English fallback.</param>
/// <param name="ReasonArgs">Optional format arguments paired with <paramref name="ReasonKey"/>.</param>
public sealed record FeatureAvailability(
    FeatureStatus Status,
    string? Reason = null,
    int? RequiredBuild = null,
    string? ReasonKey = null,
    IReadOnlyList<object?>? ReasonArgs = null);
