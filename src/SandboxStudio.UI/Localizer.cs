using Microsoft.Windows.ApplicationModel.Resources;

namespace SandboxStudio.UI;

/// <summary>
/// Thin wrapper over MRT Core <see cref="ResourceLoader"/> for code-behind strings.
/// XAML chrome strings resolve through x:Uid; these helpers cover dynamic strings.
/// </summary>
internal static class Localizer
{
    private static readonly ResourceLoader Loader = new();

    /// <summary>
    /// Languages the app ships resources for — matches the <c>Strings/*</c>
    /// folders. <c>ManifestLanguages</c> cannot be used here: it returns an
    /// empty list in unpackaged apps, which the portable build is.
    /// </summary>
    public static IReadOnlyList<string> SupportedLanguageTags { get; } = ["en-US", "es-ES"];

    /// <summary>Returns whether <paramref name="tag"/> names a shipped language.</summary>
    public static bool IsSupportedLanguage(string? tag) =>
        !string.IsNullOrEmpty(tag) &&
        SupportedLanguageTags.Any(supported => string.Equals(supported, tag, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Resolves "system default" to a concrete tag: the first OS-preferred
    /// language the app ships, matched exactly and then by language root
    /// (for example <c>es-MX</c> → <c>es-ES</c>), falling back to English.
    /// </summary>
    public static string ResolveSystemTag()
    {
        var osLanguages = Windows.System.UserProfile.GlobalizationPreferences.Languages;
        return osLanguages.FirstOrDefault(IsSupportedLanguage)
               ?? osLanguages
                   .Select(os => SupportedLanguageTags.FirstOrDefault(supported =>
                       string.Equals(LanguageRoot(supported), LanguageRoot(os), StringComparison.OrdinalIgnoreCase)))
                   .FirstOrDefault(match => match is not null)
               ?? SupportedLanguageTags[0];
    }

    private static string LanguageRoot(string tag) => tag.Split('-')[0];

    /// <summary>
    /// The language the app is actually running in: this session's override, or
    /// — for the packaged build, where the persisted override lives in the inbox
    /// API — the first resolved language, else the OS-resolved tag.
    /// </summary>
    public static string RunningLanguageTag()
    {
        // The WASDK Languages list delegates to the inbox API and ignores the
        // in-process override used by unpackaged apps, so check it first.
        var sessionOverride = Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride;
        if (!string.IsNullOrEmpty(sessionOverride))
        {
            return sessionOverride;
        }

        if (AppDistribution.IsPackaged &&
            Microsoft.Windows.Globalization.ApplicationLanguages.Languages.FirstOrDefault() is { } resolved)
        {
            return resolved;
        }

        return ResolveSystemTag();
    }

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
