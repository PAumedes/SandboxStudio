using Microsoft.UI.Xaml;

namespace SandboxStudio.UI;

/// <summary>
/// Maps the stored theme setting to a XAML theme.
/// </summary>
internal static class AppTheme
{
    /// <summary>
    /// Returns the <see cref="ElementTheme"/> for a stored theme value:
    /// <c>"Light"</c>, <c>"Dark"</c>, or anything else to follow the OS.
    /// </summary>
    public static ElementTheme FromSetting(string? theme) => theme switch
    {
        "Light" => ElementTheme.Light,
        "Dark" => ElementTheme.Dark,
        _ => ElementTheme.Default
    };
}
