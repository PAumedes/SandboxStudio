using SandboxStudio.Core.Models;

namespace SandboxStudio.Core.Execution;

/// <summary>
/// Service contract for executing and managing Windows Sandbox instances.
/// </summary>
public interface ISandboxLauncher
{
    /// <summary>
    /// Checks whether the Windows Sandbox feature is installed and available on this host.
    /// </summary>
    bool IsSandboxAvailable();

    /// <summary>
    /// Launches Windows Sandbox using the given configuration.
    /// </summary>
    /// <param name="configuration">Configuration to launch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<SandboxLaunchResult> LaunchAsync(WsbConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Launches Windows Sandbox using a saved user profile.
    /// </summary>
    /// <param name="profile">Profile to launch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<SandboxLaunchResult> LaunchProfileAsync(SandboxProfile profile, CancellationToken cancellationToken = default);
}
