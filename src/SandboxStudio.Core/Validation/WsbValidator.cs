using SandboxStudio.Core.Models;

namespace SandboxStudio.Core.Validation;

/// <summary>
/// Validates Windows Sandbox configurations against Microsoft specifications and best practices.
/// </summary>
public sealed class WsbValidator : IWsbValidator
{
    private static readonly HashSet<string> ValidTriStates = new(StringComparer.Ordinal)
    {
        "Default",
        "Enable",
        "Disable"
    };

    private const int MinMemoryMb = 256;
    private const int RecommendedMinMemoryMb = 1024;
    private const int MaxCommandLineLength = 8191;

    /// <inheritdoc/>
    public ValidationResult Validate(WsbConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var result = new ValidationResult();

        ValidateMemory(configuration.MemoryInMB, result);
        ValidateTriState(nameof(configuration.VGpu), configuration.VGpu, result);
        ValidateTriState(nameof(configuration.Networking), configuration.Networking, result);
        ValidateTriState(nameof(configuration.AudioInput), configuration.AudioInput, result);
        ValidateTriState(nameof(configuration.VideoInput), configuration.VideoInput, result);
        ValidateTriState(nameof(configuration.ProtectedClient), configuration.ProtectedClient, result);
        ValidateTriState(nameof(configuration.ClipboardRedirection), configuration.ClipboardRedirection, result);
        ValidateTriState(nameof(configuration.PrinterRedirection), configuration.PrinterRedirection, result);

        ValidateMappedFolders(configuration.MappedFolders, result);
        ValidateLogonCommand(configuration.LogonCommand, result);

        return result;
    }

    private static void ValidateMemory(int? memoryMb, ValidationResult result)
    {
        if (!memoryMb.HasValue)
        {
            return;
        }

        if (memoryMb.Value < MinMemoryMb)
        {
            result.AddError(
                nameof(WsbConfiguration.MemoryInMB),
                $"Memory allocation of {memoryMb.Value} MB is below the absolute minimum required ({MinMemoryMb} MB).");
        }
        else if (memoryMb.Value < RecommendedMinMemoryMb)
        {
            result.AddWarning(
                nameof(WsbConfiguration.MemoryInMB),
                $"Memory allocation of {memoryMb.Value} MB is below the recommended minimum of {RecommendedMinMemoryMb} MB; performance may be severely degraded.");
        }
    }

    private static readonly char[] WildcardChars = { '*', '?' };

    private static void ValidateTriState(string propertyName, string? value, ValidationResult result)
    {
        if (value is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            result.AddError(propertyName, $"{propertyName} cannot be empty or whitespace.");
            return;
        }

        if (!ValidTriStates.Contains(value))
        {
            result.AddError(
                propertyName,
                $"Invalid value '{value}' for {propertyName}. Allowed values are 'Enable', 'Disable', or 'Default'.");
        }
    }

    private static void ValidateMappedFolders(List<WsbMappedFolder> folders, ValidationResult result)
    {
        var seenHostFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenSandboxFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < folders.Count; i++)
        {
            var folder = folders[i];
            var prefix = $"{nameof(WsbConfiguration.MappedFolders)}[{i}]";

            if (string.IsNullOrWhiteSpace(folder.HostFolder))
            {
                result.AddError($"{prefix}.HostFolder", "HostFolder path cannot be empty.");
            }
            else
            {
                var invalidChars = Path.GetInvalidPathChars();
                if (folder.HostFolder.IndexOfAny(invalidChars) >= 0 || folder.HostFolder.IndexOfAny(WildcardChars) >= 0)
                {
                    result.AddError($"{prefix}.HostFolder", $"HostFolder path '{folder.HostFolder}' contains invalid path characters.");
                }
                else if (!Path.IsPathFullyQualified(folder.HostFolder))
                {
                    result.AddError($"{prefix}.HostFolder", $"HostFolder path '{folder.HostFolder}' must be a fully qualified absolute path.");
                }
                else if (!seenHostFolders.Add(folder.HostFolder))
                {
                    result.AddError($"{prefix}.HostFolder", $"Duplicate HostFolder mapping detected: '{folder.HostFolder}'.");
                }
                else if (!Directory.Exists(folder.HostFolder))
                {
                    result.AddWarning($"{prefix}.HostFolder", $"HostFolder directory '{folder.HostFolder}' does not exist on the host. Windows Sandbox will fail to start unless created.");
                }
            }

            if (!string.IsNullOrWhiteSpace(folder.SandboxFolder))
            {
                var invalidChars = Path.GetInvalidPathChars();
                if (folder.SandboxFolder.IndexOfAny(invalidChars) >= 0 || folder.SandboxFolder.IndexOfAny(WildcardChars) >= 0)
                {
                    result.AddError($"{prefix}.SandboxFolder", $"SandboxFolder path '{folder.SandboxFolder}' contains invalid path characters.");
                }
                else if (!Path.IsPathFullyQualified(folder.SandboxFolder))
                {
                    result.AddError($"{prefix}.SandboxFolder", $"SandboxFolder path '{folder.SandboxFolder}' must be a fully qualified absolute path.");
                }
                else if (!seenSandboxFolders.Add(folder.SandboxFolder))
                {
                    result.AddError($"{prefix}.SandboxFolder", $"Duplicate SandboxFolder destination detected: '{folder.SandboxFolder}'.");
                }
            }

            if (!string.Equals(folder.ReadOnly, "true", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(folder.ReadOnly, "false", StringComparison.OrdinalIgnoreCase))
            {
                result.AddError($"{prefix}.ReadOnly", $"ReadOnly value must be 'true' or 'false', found '{folder.ReadOnly}'.");
            }
        }
    }

    private static void ValidateLogonCommand(WsbLogonCommand? logonCommand, ValidationResult result)
    {
        if (logonCommand is null || string.IsNullOrWhiteSpace(logonCommand.Command))
        {
            return;
        }

        if (logonCommand.Command.Length > MaxCommandLineLength)
        {
            result.AddError(
                nameof(WsbConfiguration.LogonCommand),
                $"Logon command length ({logonCommand.Command.Length}) exceeds the Windows maximum command length limit of {MaxCommandLineLength} characters.");
        }
    }
}
