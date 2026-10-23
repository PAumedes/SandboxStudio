namespace SandboxStudio.Core.Capabilities;

/// <summary>
/// Effective availability of a <see cref="SandboxFeature"/> on the current host.
/// </summary>
public enum FeatureStatus
{
    /// <summary>The host supports the feature and no policy blocks it.</summary>
    Available,

    /// <summary>The host OS build is older than the feature requires.</summary>
    RequiresNewerWindows,

    /// <summary>An administrator disabled the feature through Windows Sandbox policy.</summary>
    DisabledByPolicy
}
