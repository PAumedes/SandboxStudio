namespace SandboxStudio.Core.Storage;

/// <summary>
/// Application-level settings persisted outside the profile store.
/// </summary>
public sealed record AppSettings
{
    /// <summary>
    /// BCP-47 language tag that overrides the operating-system display language
    /// (for example <c>en-US</c> or <c>es-ES</c>). <see langword="null"/> or empty
    /// means follow the OS language.
    /// </summary>
    public string? LanguageTag { get; init; }

    /// <summary>
    /// Application theme override: <c>"Light"</c> or <c>"Dark"</c>;
    /// <see langword="null"/> or empty means follow the operating system.
    /// </summary>
    public string? Theme { get; init; }

    /// <summary>
    /// Whether the user dismissed the first-run quick-start guide ("don't show again").
    /// </summary>
    public bool QuickStartDismissed { get; init; }
}
