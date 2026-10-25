namespace SandboxStudio.Core.Storage;

/// <summary>
/// Host folders that Windows Sandbox itself reads: mapped logon scripts and the
/// <c>.wsb</c> files handed to <c>WindowsSandbox.exe</c>. They live under
/// <c>%USERPROFILE%\.sandboxstudio</c>, outside AppData, because Windows Sandbox
/// fails to map AppData folders and the Store build's new AppData files are
/// redirected to a package-private location that other processes cannot see.
/// </summary>
public static class SharedDataPaths
{
    /// <summary>Gets the root folder, <c>%USERPROFILE%\.sandboxstudio</c>.</summary>
    public static string Root => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".sandboxstudio");

    /// <summary>Gets the folder holding each script-backed profile's logon script directory.</summary>
    public static string Scripts => Path.Combine(Root, "Scripts");

    /// <summary>Gets the folder for the temporary <c>.wsb</c> files of legacy (pre-CLI) launches.</summary>
    public static string Runs => Path.Combine(Root, "Runs");
}
