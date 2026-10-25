using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;

namespace SandboxStudio.UI;

/// <summary>
/// Tells the two distributions apart: the Microsoft Store MSIX (packaged, updated
/// by the Store) and the portable single-file exe (unpackaged, updated from GitHub).
/// </summary>
internal static class AppDistribution
{
    /// <summary>
    /// Gets whether the process runs with package identity (the Store build).
    /// </summary>
    public static bool IsPackaged { get; } = DetectPackageIdentity();

    /// <summary>
    /// Returns the <c>.wsb</c> file the app was launched to open, if any: a file
    /// activation for the Store build, or the first command-line argument for
    /// the portable exe (e.g. Explorer's "Open with").
    /// </summary>
    public static string? GetLaunchFilePath()
    {
        string? path = null;
        if (IsPackaged)
        {
            var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
            if (activation.Kind == ExtendedActivationKind.File &&
                activation.Data is IFileActivatedEventArgs { Files.Count: > 0 } file)
            {
                path = file.Files[0].Path;
            }
        }
        else
        {
            path = Environment.GetCommandLineArgs().Skip(1).FirstOrDefault();
        }

        return path is not null &&
               string.Equals(Path.GetExtension(path), ".wsb", StringComparison.OrdinalIgnoreCase) &&
               File.Exists(path)
            ? path
            : null;
    }

    private static bool DetectPackageIdentity()
    {
        try
        {
            return Windows.ApplicationModel.Package.Current is not null;
        }
        catch (InvalidOperationException)
        {
            // Thrown when the process has no package identity (the portable exe).
            return false;
        }
    }
}
