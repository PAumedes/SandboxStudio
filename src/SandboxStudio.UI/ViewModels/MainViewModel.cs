using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SandboxStudio.Core.Capabilities;
using SandboxStudio.Core.Execution;
using SandboxStudio.Core.Models;
using SandboxStudio.Core.Scripts;
using SandboxStudio.Core.Storage;

namespace SandboxStudio.UI.ViewModels;

/// <summary>
/// ViewModel governing the main dashboard, profile cards, and quick actions.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly IProfileRepository _repository;
    private readonly ILogonScriptService? _scripts;
    private readonly ISandboxLauncher _launcher;
    private readonly IHostCapabilities _capabilities;
    private readonly List<SandboxProfile> _allProfiles = new();

    /// <summary>
    /// Gets the profiles displayed on the dashboard, filtered by <see cref="FilterText"/>.
    /// </summary>
    public ObservableCollection<SandboxProfile> Profiles { get; } = new();

    /// <summary>
    /// Gets or sets the tag filter text; profiles must contain every entered tag to show.
    /// </summary>
    [ObservableProperty]
    public partial string FilterText { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the active tag filter matches no profiles.
    /// </summary>
    [ObservableProperty]
    public partial bool HasNoMatchingProfiles { get; set; }

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
    /// Gets or sets the InfoBar title shown when Windows Sandbox cannot be used, or null to keep the default.
    /// </summary>
    [ObservableProperty]
    public partial string? SandboxUnavailableTitle { get; set; }

    /// <summary>
    /// Initializes a new instance of <see cref="MainViewModel"/>.
    /// </summary>
    public MainViewModel(
        IProfileRepository repository,
        ISandboxLauncher launcher,
        IHostCapabilities capabilities,
        ILogonScriptService? scripts = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        _capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
        _scripts = scripts;

        var status = capabilities.SandboxStatus;
        IsSandboxInstalled = status == HostSandboxStatus.Available;
        SandboxStatusMessage = status switch
        {
            HostSandboxStatus.Available => Localizer.Get("SandboxStatusReady"),
            HostSandboxStatus.UnsupportedEdition => Localizer.Get("SandboxStatusEditionRequired"),
            HostSandboxStatus.VirtualizationDisabled => Localizer.Get("SandboxStatusVirtDisabled"),
            _ => Localizer.Get("SandboxStatusUnavailable")
        };
        SandboxUnavailableReason = status switch
        {
            HostSandboxStatus.UnsupportedEdition => Localizer.Get("SandboxInfoEditionRequired"),
            HostSandboxStatus.NotInstalled => Localizer.Get("SandboxInfoNotInstalled"),
            HostSandboxStatus.VirtualizationDisabled => Localizer.Get("SandboxInfoVirtDisabled"),
            _ => null
        };
        SandboxUnavailableTitle = status switch
        {
            HostSandboxStatus.UnsupportedEdition => Localizer.Get("SandboxStatusEditionUnsupported"),
            HostSandboxStatus.VirtualizationDisabled => Localizer.Get("SandboxStatusVirtDisabledShort"),
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
        StatusMessage = Localizer.Get("StatusLoadingProfiles");

        try
        {
            await _repository.EnsureDefaultPresetsAsync();
            var items = await _repository.GetAllAsync();

            // Sweep script files orphaned by deletes or failed saves.
            _scripts?.PruneOrphans(items.Select(p => p.Id).ToHashSet());

            _allProfiles.Clear();
            _allProfiles.AddRange(items);
            ApplyFilter();

            StatusMessage = HasActiveTagFilter
                ? Localizer.Format("StatusProfilesFiltered", Profiles.Count, _allProfiles.Count)
                : Localizer.Format("StatusProfilesReady", Profiles.Count);
        }
        catch (Exception ex)
        {
            StatusMessage = Localizer.Format("StatusProfilesLoadError", ex.Message);
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
            Name = Localizer.Format("NewProfileDefaultName", _allProfiles.Count + 1),
            Description = Localizer.Get("NewProfileDefaultDescription")
        };

        try
        {
            await _repository.SaveAsync(newProfile);
            _allProfiles.Add(newProfile);
            FilterText = string.Empty; // clear the filter so the new card is visible
            ApplyFilter();
            SelectedProfile = newProfile;
            StatusMessage = Localizer.Format("StatusProfileCreated", newProfile.Name);
        }
        catch (Exception ex)
        {
            StatusMessage = Localizer.Format("StatusProfileCreateError", ex.Message);
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

        StatusMessage = Localizer.Format("StatusLaunching", profile.Name);

        var result = await _launcher.LaunchProfileAsync(profile);
        if (result.IsSuccess)
        {
            var sessionNote = result.SessionId is { } sessionId
                ? Localizer.Format("LaunchSessionNote", sessionId.ToString("N")[..8])
                : string.Empty;
            var overrides = _capabilities.GetPolicyOverrides(profile.Configuration);
            StatusMessage = overrides.Count == 0
                ? Localizer.Format("StatusLaunchSuccess", profile.Name, sessionNote)
                : Localizer.Format("StatusLaunchPolicyOverrides", profile.Name, sessionNote, string.Join("; ", overrides.Select(o => o.SettingName).Distinct()));
        }
        else
        {
            StatusMessage = Localizer.Format("StatusLaunchFailed", result.ErrorMessage);
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
            _allProfiles.Remove(profile);
            ApplyFilter();
            StatusMessage = Localizer.Format("StatusProfileDeleted", profile.Name);
        }
    }

    /// <summary>
    /// Adds a profile persisted outside this ViewModel (for example a duplicate) to the dashboard.
    /// </summary>
    public void TrackProfile(SandboxProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        _allProfiles.Add(profile);
        ApplyFilter();
    }

    partial void OnFilterTextChanged(string value) => ApplyFilter();

    private bool HasActiveTagFilter => ProfileTags.Parse(FilterText).Count > 0;

    private void ApplyFilter()
    {
        var requiredTags = ProfileTags.Parse(FilterText);
        Profiles.Clear();
        foreach (var profile in _allProfiles.Where(
                     p => ProfileTags.MatchesAll(p.Tags, requiredTags)))
        {
            Profiles.Add(profile);
        }

        HasNoMatchingProfiles = Profiles.Count == 0 && requiredTags.Count > 0;
        if (requiredTags.Count > 0)
        {
            StatusMessage = Localizer.Format("StatusTagFilterMatch", Profiles.Count, _allProfiles.Count);
        }
    }
}
