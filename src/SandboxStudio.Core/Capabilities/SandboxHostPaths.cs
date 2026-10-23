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

    /// <summary>
    /// The Windows Sandbox CLI shipped with the feature on Windows 11 24H2+.
    /// The Sandbox package is MSIX-delivered, so the CLI lives under System32 on
    /// some builds but only as an App Execution Alias in WindowsApps on others.
    /// </summary>
    internal static readonly string WsbExe = ResolveWsbExe();

    private static string ResolveWsbExe()
    {
        var system32 = Path.Combine(ResolveSystemDirectory(), "wsb.exe");
        if (File.Exists(system32))
        {
            return system32;
        }

        // App Execution Alias: %LOCALAPPDATA%\Microsoft\WindowsApps\wsb.exe
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"Microsoft\WindowsApps\wsb.exe");
    }

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
