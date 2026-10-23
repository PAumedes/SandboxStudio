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

    /// <summary>
    /// Renders a Core-emitted message: when <paramref name="key"/> resolves to a
    /// resource the text is formatted with <paramref name="args"/>; otherwise the
    /// Core-provided <paramref name="fallback"/> (English) is returned. Arg/placeholder
    /// mismatches degrade to the fallback instead of throwing.
    /// </summary>
    public static string Core(string? key, IReadOnlyList<object?>? args, string? fallback)
    {
        if (key is null)
        {
            return fallback ?? string.Empty;
        }

        var template = Loader.GetString(key);
        if (string.IsNullOrEmpty(template))
        {
            return fallback ?? string.Empty;
        }

        if (args is not { Count: > 0 })
        {
            return template.Contains('{') ? fallback ?? string.Empty : template;
        }

        try
        {
            return string.Format(template, [.. args]);
        }
        catch (FormatException)
        {
            return fallback ?? string.Empty;
        }
    }
}
