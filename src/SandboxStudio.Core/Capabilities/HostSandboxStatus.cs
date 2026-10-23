namespace SandboxStudio.Core.Capabilities;

/// <summary>
/// Overall Windows Sandbox availability on the host machine.
/// </summary>
public enum HostSandboxStatus
{
    /// <summary>Windows Sandbox is installed and the edition supports it.</summary>
    Available,

    /// <summary>Windows Sandbox is not installed or the optional feature is disabled.</summary>
    NotInstalled,

    /// <summary>The Windows edition does not support Windows Sandbox (requires Pro, Enterprise, or Education).</summary>
    UnsupportedEdition,

    /// <summary>Windows Sandbox is installed but hardware virtualization is disabled in firmware or unavailable.</summary>
    VirtualizationDisabled
}
