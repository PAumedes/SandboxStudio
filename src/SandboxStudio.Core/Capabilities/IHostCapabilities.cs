using SandboxStudio.Core.Models;

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

    /// <summary>
    /// Determines which configured settings a host policy will silently override at run time.
    /// Settings left unset (<see langword="null"/>) are reported too whenever the feature
    /// defaults to enabled on this host, since omitting the element requests the default.
    /// </summary>
    /// <param name="configuration">The profile configuration to evaluate.</param>
    /// <returns>One entry per setting whose requested behavior host policy disables.</returns>
    IReadOnlyList<PolicyOverride> GetPolicyOverrides(WsbConfiguration configuration);
}
