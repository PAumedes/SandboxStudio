namespace SandboxStudio.Core.Sessions;

/// <summary>
/// Reported state of a running or stopped Windows Sandbox session.
/// </summary>
public enum SandboxSessionState
{
    /// <summary>The session's state was not reported by the CLI.</summary>
    Unknown,

    /// <summary>The session is running.</summary>
    Running,

    /// <summary>The session has been stopped.</summary>
    Stopped
}
