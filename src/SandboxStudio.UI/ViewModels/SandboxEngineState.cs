namespace SandboxStudio.UI.ViewModels;

/// <summary>
/// Status-light states for the Windows Sandbox engine shown in the status bar.
/// </summary>
public enum SandboxEngineState
{
    /// <summary>Windows Sandbox is installed and fully usable.</summary>
    Ready,

    /// <summary>Windows Sandbox runs, but host policies restrict some features.</summary>
    Degraded,

    /// <summary>Windows Sandbox cannot run on this host.</summary>
    Unavailable
}
