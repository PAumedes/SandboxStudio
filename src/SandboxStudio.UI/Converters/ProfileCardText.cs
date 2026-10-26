using Microsoft.UI.Xaml;

namespace SandboxStudio.UI.Converters;

/// <summary>
/// Display text for profile cards, used through x:Bind function bindings (a
/// converter cannot be used from x:Bind inside a Window's DataTemplate).
/// </summary>
public static class ProfileCardText
{
    /// <summary>
    /// Returns the localized label for a Networking tri-state ("Enable",
    /// "Disable", "Default" or unset).
    /// </summary>
    public static string Networking(string? value) => value switch
    {
        "Enable" => Localizer.Get("CardNetworkingEnabled"),
        "Disable" => Localizer.Get("CardNetworkingDisabled"),
        _ => Localizer.Get("CardNetworkingDefault")
    };

    /// <summary>
    /// Returns the localized label for a vGPU tri-state ("Enable",
    /// "Disable", "Default" or unset).
    /// </summary>
    public static string VGpu(string? value) => value switch
    {
        "Enable" => Localizer.Get("CardGpuEnabled"),
        "Disable" => Localizer.Get("CardGpuDisabled"),
        _ => Localizer.Get("CardGpuDefault")
    };

    /// <summary>
    /// Returns the localized memory label: whole gigabytes stay readable
    /// ("RAM: 8 GB"), other values keep megabytes ("RAM: 512 MB"), and an
    /// unset value reports the sandbox default.
    /// </summary>
    public static string Memory(int? megabytes) => megabytes switch
    {
        { } mb when mb > 0 && mb % 1024 == 0 => Localizer.Format("CardMemoryGb", mb / 1024),
        { } mb => Localizer.Format("CardMemoryMb", mb),
        _ => Localizer.Get("CardMemoryDefault")
    };

    /// <summary>
    /// Returns the localized mapped-folders chip label ("Folders: 2").
    /// </summary>
    public static string MappedFolders(int count) => Localizer.Format("CardMappedFolders", count);

    /// <summary>
    /// Shows the mapped-folders chip only when the profile maps at least one folder.
    /// </summary>
    public static Visibility MappedFoldersVisibility(int count) =>
        count > 0 ? Visibility.Visible : Visibility.Collapsed;
}
