namespace SandboxStudio.Core.Capabilities;

/// <summary>
/// Describes a profile setting whose requested behavior a Windows Sandbox host
/// policy disables at run time.
/// </summary>
/// <param name="SettingName">The configuration property being overridden.</param>
/// <param name="PolicyValueName">The registry value name of the overriding policy.</param>
/// <param name="Message">Human-readable explanation suitable for display.</param>
public sealed record PolicyOverride(
    string SettingName,
    string PolicyValueName,
    string Message);
