namespace SandboxStudio.Core.Capabilities;

/// <summary>
/// Well-known file system locations of the Windows Sandbox feature on the host.
/// </summary>
internal static class SandboxHostPaths
{
    /// <summary>The inbox Windows Sandbox executable installed by the optional feature.</summary>
    internal static readonly string WindowsSandboxExe = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System),
        "WindowsSandbox.exe");
}
