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
}
