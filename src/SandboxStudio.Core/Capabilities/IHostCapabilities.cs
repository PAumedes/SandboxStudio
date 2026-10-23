namespace SandboxStudio.Core.Capabilities;

/// <summary>
/// Reports host OS build, edition, and administrator policy state, and computes the
/// effective availability of each <see cref="SandboxFeature"/>.
/// </summary>
public interface IHostCapabilities
{
    /// <summary>Gets the host operating system build number.</summary>
    int OsBuild { get; }

    /// <summary>Gets a value indicating whether the host runs on an Arm architecture.</summary>
    bool IsArm64 { get; }

    /// <summary>Gets the reported Windows edition identifier, or <see langword="null"/> when unknown.</summary>
    string? EditionId { get; }

    /// <summary>Gets the overall Windows Sandbox availability on this host.</summary>
    HostSandboxStatus SandboxStatus { get; }

    /// <summary>
    /// Computes the effective availability of a feature: supported by the OS build
    /// and not blocked by a Windows Sandbox policy.
    /// </summary>
    /// <param name="feature">The capability to evaluate.</param>
    FeatureAvailability GetAvailability(SandboxFeature feature);
}
