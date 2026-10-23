namespace SandboxStudio.Core.Capabilities;

/// <summary>
/// Describes a profile setting whose requested behavior a Windows Sandbox host
/// policy disables at run time.
/// </summary>
/// <param name="SettingName">The configuration property being overridden.</param>
/// <param name="PolicyValueName">The registry value name of the overriding policy.</param>
/// <param name="Message">Human-readable explanation suitable for display.</param>
/// <param name="MessageKey">Optional <see cref="Localization.MessageKeys"/> entry identifying the
/// message template so hosts can localize it; <paramref name="Message"/> remains the English fallback.</param>
/// <param name="MessageArgs">Optional format arguments paired with <paramref name="MessageKey"/>.</param>
public sealed record PolicyOverride(
    string SettingName,
    string PolicyValueName,
    string Message,
    string? MessageKey = null,
    IReadOnlyList<object?>? MessageArgs = null);
