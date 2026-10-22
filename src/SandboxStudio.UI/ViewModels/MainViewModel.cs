using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SandboxStudio.Core.Execution;
using SandboxStudio.Core.Models;
using SandboxStudio.Core.Storage;

namespace SandboxStudio.UI.ViewModels;

/// <summary>
/// ViewModel governing the main dashboard, profile cards, and quick actions.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IProfileRepository _repository;
    private readonly ISandboxLauncher _launcher;

    [ObservableProperty]
    public partial ObservableCollection<SandboxProfile> Profiles { get; set; } = new();

    [ObservableProperty]
    public partial SandboxProfile? SelectedProfile { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    [ObservableProperty]
    public partial bool IsSandboxInstalled { get; set; }

    /// <summary>
    /// Initializes a new instance of <see cref="MainViewModel"/>.
    /// </summary>
    public MainViewModel(IProfileRepository repository, ISandboxLauncher launcher)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));

        IsSandboxInstalled = _launcher.IsSandboxAvailable();
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
            StatusMessage = $"'{profile.Name}' launched successfully.";
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
