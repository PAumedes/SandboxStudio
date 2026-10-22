namespace SandboxStudio.Core.Models;

/// <summary>
/// Tri-state configuration values accepted by Windows Sandbox.
/// </summary>
public enum TriState
{
    /// <summary>System default behavior.</summary>
    Default,

    /// <summary>Explicitly enabled.</summary>
    Enable,

    /// <summary>Explicitly disabled.</summary>
    Disable
}
