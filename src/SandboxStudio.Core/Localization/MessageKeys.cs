namespace SandboxStudio.Core.Localization;

/// <summary>
/// Stable keys identifying user-facing text composed by Core services. Each value
/// matches a resource key in the UI's <c>Strings/*/Resources.resw</c> files; the
/// parity tests enforce that every key resolves in every shipped language.
/// </summary>
public static class MessageKeys
{
    /// <summary>"Requires Windows build {0} or later (host build: {1})."</summary>
    public const string ReasonRequiresBuild = "CoreReasonRequiresBuild";

    /// <summary>"Disabled by host policy '{0}'."</summary>
    public const string ReasonDisabledByPolicy = "CoreReasonDisabledByPolicy";

    /// <summary>"MappedFolders has no effect because host policy '{0}' disables it."</summary>
    public const string PolicyMappedFoldersDisabled = "CorePolicyMappedFoldersDisabled";

    /// <summary>"Writable mapped folders have no effect because host policy '{0}' disables write access."</summary>
    public const string PolicyWritableFoldersDisabled = "CorePolicyWritableFoldersDisabled";

    /// <summary>"{0} is not set and defaults to enabled, but host policy '{1}' disables it."</summary>
    public const string PolicyDefaultConflict = "CorePolicyDefaultConflict";

    /// <summary>"{0} is set to '{1}' but host policy '{2}' disables it."</summary>
    public const string PolicySettingConflict = "CorePolicySettingConflict";

    /// <summary>"Memory allocation of {0} MB is below the absolute minimum required ({1} MB)."</summary>
    public const string MemoryBelowMinimum = "CoreMemoryBelowMinimum";

    /// <summary>"Memory allocation of {0} MB is below the recommended minimum of {1} MB."</summary>
    public const string MemoryBelowRecommended = "CoreMemoryBelowRecommended";

    /// <summary>"{0} cannot be empty or whitespace."</summary>
    public const string ValueEmpty = "CoreValueEmpty";

    /// <summary>"Invalid value '{0}' for {1}. Allowed values are 'Enable', 'Disable', or 'Default'."</summary>
    public const string InvalidTriState = "CoreInvalidTriState";

    /// <summary>"HostFolder path cannot be empty."</summary>
    public const string HostFolderEmpty = "CoreHostFolderEmpty";

    /// <summary>"{0} path '{1}' contains invalid path characters."</summary>
    public const string PathInvalidChars = "CorePathInvalidChars";

    /// <summary>"{0} path '{1}' must be a fully qualified absolute path."</summary>
    public const string PathNotQualified = "CorePathNotQualified";

    /// <summary>"Duplicate HostFolder mapping detected: '{0}'."</summary>
    public const string HostFolderDuplicate = "CoreHostFolderDuplicate";

    /// <summary>"HostFolder directory '{0}' does not exist on the host. Windows Sandbox will fail to start unless created."</summary>
    public const string HostFolderMissing = "CoreHostFolderMissing";

    /// <summary>"Duplicate SandboxFolder destination detected: '{0}'."</summary>
    public const string SandboxFolderDuplicate = "CoreSandboxFolderDuplicate";

    /// <summary>"ReadOnly value must be 'true' or 'false', found '{0}'."</summary>
    public const string ReadOnlyInvalid = "CoreReadOnlyInvalid";

    /// <summary>"Logon command length ({0}) exceeds the Windows maximum command length limit of {1} characters."</summary>
    public const string LogonTooLong = "CoreLogonTooLong";

    /// <summary>"The Windows Sandbox CLI is not available on this host."</summary>
    public const string CliUnavailable = "CoreCliUnavailable";

    /// <summary>"The configuration is too large for a wsb start command line."</summary>
    public const string CliConfigTooLarge = "CoreCliConfigTooLarge";

    /// <summary>"The Windows Sandbox CLI did not report a sandbox ID."</summary>
    public const string CliNoSandboxId = "CoreCliNoSandboxId";

    /// <summary>"The Windows Sandbox CLI did not report an exit code."</summary>
    public const string CliNoExitCode = "CoreCliNoExitCode";

    /// <summary>"The Windows Sandbox CLI returned output that could not be parsed."</summary>
    public const string CliUnparsable = "CoreCliUnparsable";

    /// <summary>"wsb {0} failed with exit code {1}."</summary>
    public const string CliExitFailure = "CoreCliExitFailure";

    /// <summary>"wsb {0} failed: {1}" — carries raw CLI stderr.</summary>
    public const string CliStdErrFailure = "CoreCliStdErrFailure";

    /// <summary>"wsb {0} did not finish within {1} seconds; the operation may still have completed."</summary>
    public const string CliTimeout = "CoreCliTimeout";

    /// <summary>"Validation failed: {0} - {1}"</summary>
    public const string LauncherValidationFailed = "CoreLauncherValidationFailed";

    /// <summary>"Failed to launch Windows Sandbox."</summary>
    public const string LauncherFailed = "CoreLauncherFailed";

    /// <summary>"Failed to launch Windows Sandbox: {0}" — carries the exception message.</summary>
    public const string LauncherException = "CoreLauncherException";

    /// <summary>"Windows Sandbox is not installed or enabled on this host."</summary>
    public const string LauncherNotInstalled = "CoreLauncherNotInstalled";
}
