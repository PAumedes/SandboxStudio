using Microsoft.Windows.ApplicationModel.Resources;

namespace SandboxStudio.UI;

/// <summary>
/// Thin wrapper over MRT Core <see cref="ResourceLoader"/> for code-behind strings.
/// XAML chrome strings resolve through x:Uid; these helpers cover dynamic strings.
/// </summary>
internal static class Localizer
{
    private static readonly ResourceLoader Loader = new();

    /// <summary>Returns the localized string for <paramref name="key"/>, or the key when missing.</summary>
    public static string Get(string key)
    {
        var value = Loader.GetString(key);
        return string.IsNullOrEmpty(value) ? key : value;
    }

    /// <summary>Returns the localized format string for <paramref name="key"/> with <paramref name="args"/> applied.</summary>
    public static string Format(string key, params object?[] args) =>
        string.Format(Get(key), args);
}
