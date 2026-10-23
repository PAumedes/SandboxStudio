namespace SandboxStudio.Core.Capabilities;

/// <summary>
/// Well-known file system locations of the Windows Sandbox feature on the host.
/// </summary>
internal static class SandboxHostPaths
{
    /// <summary>The inbox Windows Sandbox executable installed by the optional feature.</summary>
    internal static readonly string WindowsSandboxExe = Path.Combine(
        ResolveSystemDirectory(),
        "WindowsSandbox.exe");

    /// <summary>The Windows Sandbox CLI shipped with the feature on Windows 11 24H2+.</summary>
    internal static readonly string WsbExe = Path.Combine(
        ResolveSystemDirectory(),
        "wsb.exe");

    private static string ResolveSystemDirectory()
    {
        // WindowsSandbox.exe only exists in the native System32. Under WOW64 a 32-bit
        // process sees SysWOW64, so bypass file-system redirection via the Sysnative alias.
        if (!Environment.Is64BitProcess && Environment.Is64BitOperatingSystem)
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "Sysnative");
        }

        return Environment.GetFolderPath(Environment.SpecialFolder.System);
    }
}
