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
    private readonly IWsbSerializer _serializer;
    private readonly IWsbValidator _validator;
    private readonly IProfileRepository? _profileRepository;
    private readonly string _tempDirectory;

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
        string? tempDirectory = null)
    {
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _profileRepository = profileRepository;
        _tempDirectory = tempDirectory ?? Path.Combine(Path.GetTempPath(), "SandboxStudio", "Runs");

        if (!Directory.Exists(_tempDirectory))
        {
            Directory.CreateDirectory(_tempDirectory);
        }
    }

    /// <inheritdoc/>
    public bool IsSandboxAvailable() => File.Exists(WindowsSandboxExePath);

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

        var runId = Guid.NewGuid();
        var tempFilePath = Path.Combine(_tempDirectory, $"Sandbox_{runId:N}.wsb");

        try
        {
            await _serializer.SerializeToFileAsync(configuration, tempFilePath, cancellationToken);

            var startInfo = new ProcessStartInfo
            {
                FileName = tempFilePath,
                UseShellExecute = true
            };

            var process = Process.Start(startInfo);
            int? processId = null;
            try
            {
                processId = process?.Id;
            }
            catch (InvalidOperationException)
            {
                // Process ID is not always accessible when ShellExecute launches an associated document or elevated process
            }

            return SandboxLaunchResult.Success(processId, tempFilePath);
        }
        catch (Exception ex)
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
                catch
                {
                    // Profile launch succeeded; repository update failure should not disguise launch success
                }
            }
        }

        return result;
    }
}
