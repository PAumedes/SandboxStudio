using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SandboxStudio.Core.Capabilities;
using SandboxStudio.Core.Execution;
using SandboxStudio.Core.Models;
using SandboxStudio.Core.Storage;

namespace SandboxStudio.UI.ViewModels;

/// <summary>
/// ViewModel governing the main dashboard, profile cards, and quick actions.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly IProfileRepository _repository;
    private readonly ISandboxLauncher _launcher;
    private readonly IHostCapabilities _capabilities;

    /// <summary>
    /// Gets or sets the profiles displayed on the dashboard.
    /// </summary>
    [ObservableProperty]
    public partial ObservableCollection<SandboxProfile> Profiles { get; set; } = new();

    /// <summary>
    /// Gets or sets the currently selected profile.
    /// </summary>
    [ObservableProperty]
    public partial SandboxProfile? SelectedProfile { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether profiles are being loaded.
    /// </summary>
    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    /// <summary>
    /// Gets or sets the status-bar message shown to the user.
    /// </summary>
    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether Windows Sandbox is available on this host.
    /// </summary>
    [ObservableProperty]
    public partial bool IsSandboxInstalled { get; set; }

    /// <summary>
    /// Gets or sets the engine status text shown in the status bar.
    /// </summary>
    [ObservableProperty]
    public partial string? SandboxStatusMessage { get; set; }

    /// <summary>
    /// Gets or sets the explanation shown when Windows Sandbox cannot be used, or null when it can.
    /// </summary>
    [ObservableProperty]
    public partial string? SandboxUnavailableReason { get; set; }

    /// <summary>
    /// Initializes a new instance of <see cref="MainViewModel"/>.
    /// </summary>
    public MainViewModel(
        IProfileRepository repository,
        ISandboxLauncher launcher,
        IHostCapabilities capabilities)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        _capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));

        IsSandboxInstalled = capabilities.SandboxStatus == HostSandboxStatus.Available;
        SandboxStatusMessage = capabilities.SandboxStatus switch
        {
            HostSandboxStatus.Available => "Windows Sandbox Engine Ready",
            HostSandboxStatus.UnsupportedEdition => "Windows Sandbox requires a Pro, Enterprise, or Education edition",
            _ => "Windows Sandbox Engine Unavailable"
        };
        SandboxUnavailableReason = capabilities.SandboxStatus switch
        {
            HostSandboxStatus.UnsupportedEdition =>
                "Windows Sandbox requires a Pro, Enterprise, or Education edition of Windows.",
            HostSandboxStatus.NotInstalled =>
                "Windows Sandbox is not enabled on this host. Profiles can be created, but launching them requires the Windows Sandbox optional feature.",
            _ => null
        };
    }

    /// <summary>
    /// Loads profiles asynchronously from the repository.
    /// </summary>
    [RelayCommand]
    public async Task LoadProfilesAsync()
    {
        IsLoading = true;
        StatusMessage = "Loading profiles...";

        try
        {
            await _repository.EnsureDefaultPresetsAsync();
            var items = await _repository.GetAllAsync();

            Profiles.Clear();
            foreach (var item in items)
            {
                Profiles.Add(item);
            }

            StatusMessage = $"{Profiles.Count} profiles ready.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading profiles: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Creates a new profile with default configuration and saves it to the repository.
    /// </summary>
    [RelayCommand]
    public async Task CreateProfileAsync()
    {
        var newProfile = new SandboxProfile
        {
            Name = $"New Sandbox {Profiles.Count + 1}",
            Description = "Custom sandbox configuration."
        };

        try
        {
            await _repository.SaveAsync(newProfile);
            Profiles.Add(newProfile);
            SelectedProfile = newProfile;
            StatusMessage = $"Created profile '{newProfile.Name}'.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error creating profile: {ex.Message}";
        }
    }

    /// <summary>
    /// Launches the specified profile in Windows Sandbox.
    /// </summary>
    [RelayCommand]
    public async Task LaunchProfileAsync(SandboxProfile? profile)
    {
        if (profile is null)
        {
            return;
        }

        StatusMessage = $"Launching '{profile.Name}'...";

        var result = await _launcher.LaunchProfileAsync(profile);
        if (result.IsSuccess)
        {
            var overrides = _capabilities.GetPolicyOverrides(profile.Configuration);
            StatusMessage = overrides.Count == 0
                ? $"'{profile.Name}' launched successfully."
                : $"'{profile.Name}' launched; host policy overrides: {string.Join("; ", overrides.Select(o => o.SettingName))}.";
        }
        else
        {
            StatusMessage = $"Launch failed: {result.ErrorMessage}";
        }
    }

    /// <summary>
    /// Deletes a profile from the repository.
    /// </summary>
    [RelayCommand]
    public async Task DeleteProfileAsync(SandboxProfile? profile)
    {
        if (profile is null)
        {
            return;
        }

        var deleted = await _repository.DeleteAsync(profile.Id);
        if (deleted)
        {
            Profiles.Remove(profile);
            StatusMessage = $"Deleted '{profile.Name}'.";
        }
    }
}
