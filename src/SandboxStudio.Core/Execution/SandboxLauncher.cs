using System.Diagnostics;
using SandboxStudio.Core.Models;
using SandboxStudio.Core.Serialization;
using SandboxStudio.Core.Storage;
using SandboxStudio.Core.Validation;

namespace SandboxStudio.Core.Execution;

/// <summary>
/// Orchestrates the preparation and launching of Windows Sandbox instances.
/// </summary>
public sealed class SandboxLauncher : ISandboxLauncher
{
    private static readonly TimeSpan TempFileMaxAge = TimeSpan.FromDays(1);

    private readonly IWsbSerializer _serializer;
    private readonly IWsbValidator _validator;
    private readonly IProfileRepository? _profileRepository;
    private readonly string _tempDirectory;
    private readonly Func<ProcessStartInfo, Process?> _processStarter;
    private readonly Func<bool> _sandboxAvailability;

    private static readonly string WindowsSandboxExePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System),
        "WindowsSandbox.exe");

    /// <summary>
    /// Initializes a new instance of <see cref="SandboxLauncher"/>.
    /// </summary>
    public SandboxLauncher(
        IWsbSerializer serializer,
        IWsbValidator validator,
        IProfileRepository? profileRepository = null,
        string? tempDirectory = null,
        Func<ProcessStartInfo, Process?>? processStarter = null,
        Func<bool>? sandboxAvailability = null)
    {
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _profileRepository = profileRepository;
        _tempDirectory = tempDirectory ?? Path.Combine(Path.GetTempPath(), "SandboxStudio", "Runs");
        _processStarter = processStarter ?? Process.Start;
        _sandboxAvailability = sandboxAvailability ?? (() => File.Exists(WindowsSandboxExePath));

        CleanupStaleRunFiles();
    }

    /// <inheritdoc/>
    public bool IsSandboxAvailable() => _sandboxAvailability();

    /// <inheritdoc/>
    public async Task<SandboxLaunchResult> LaunchAsync(WsbConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var validationResult = _validator.Validate(configuration);
        if (!validationResult.IsValid)
        {
            var firstError = validationResult.Messages.First(m => m.Severity == ValidationSeverity.Error);
            return SandboxLaunchResult.Failure($"Validation failed: {firstError.PropertyName} - {firstError.Message}");
        }

        if (!IsSandboxAvailable())
        {
            return SandboxLaunchResult.Failure("Windows Sandbox is not installed or enabled on this host.");
        }

        var runId = Guid.NewGuid();
        var tempFilePath = Path.Combine(_tempDirectory, $"Sandbox_{runId:N}.wsb");

        try
        {
            await _serializer.SerializeToFileAsync(configuration, tempFilePath, cancellationToken);

            var startInfo = new ProcessStartInfo
            {
                FileName = WindowsSandboxExePath,
                Arguments = $"\"{tempFilePath}\"",
                UseShellExecute = false
            };

            int? processId;
            using (var process = _processStarter(startInfo))
            {
                try
                {
                    processId = process?.Id;
                }
                catch (InvalidOperationException)
                {
                    processId = null;
                }
            }

            return SandboxLaunchResult.Success(processId, tempFilePath);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return SandboxLaunchResult.Failure($"Failed to launch Windows Sandbox: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<SandboxLaunchResult> LaunchProfileAsync(SandboxProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var result = await LaunchAsync(profile.Configuration, cancellationToken);
        if (result.IsSuccess)
        {
            profile.LastLaunchedAt = DateTimeOffset.UtcNow;
            if (_profileRepository is not null)
            {
                try
                {
                    await _profileRepository.SaveAsync(profile, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Profile launch succeeded; repository update failure should not disguise launch success
                }
            }
        }

        return result;
    }

    private void CleanupStaleRunFiles()
    {
        try
        {
            if (!Directory.Exists(_tempDirectory))
            {
                return;
            }

            var cutoff = DateTime.UtcNow - TempFileMaxAge;
            foreach (var file in Directory.EnumerateFiles(_tempDirectory, "Sandbox_*.wsb"))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(file) < cutoff)
                    {
                        File.Delete(file);
                    }
                }
                catch
                {
                    // Best-effort cleanup; a locked file must not prevent construction
                }
            }
        }
        catch
        {
            // Best-effort cleanup; enumeration failures must not prevent construction
        }
    }
}
