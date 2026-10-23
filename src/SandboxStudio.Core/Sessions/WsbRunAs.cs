namespace SandboxStudio.Core.Sessions;

/// <summary>
/// User context a <c>wsb exec</c> command runs under inside the sandbox.
/// </summary>
public enum WsbRunAs
{
    /// <summary>The currently logged-on user; requires an active remote session.</summary>
    ExistingLogin,

    /// <summary>The system context.</summary>
    System
}
