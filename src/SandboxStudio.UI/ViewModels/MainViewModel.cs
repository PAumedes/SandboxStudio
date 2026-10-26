using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SandboxStudio.Core.Capabilities;
using SandboxStudio.Core.Execution;
using SandboxStudio.Core.Models;
using SandboxStudio.Core.Scripts;
using SandboxStudio.Core.Storage;
using SandboxStudio.Core.Updates;

namespace SandboxStudio.UI.ViewModels;

/// <summary>
/// ViewModel governing the main dashboard, profile cards, and quick actions.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    /// <summary>
    /// Sandbox features controlled by a Windows Sandbox policy value; any of them
    /// disabled by policy degrades an otherwise-ready engine.
    /// </summary>
    private static readonly SandboxFeature[] PolicyGatedFeatures =
    [
        SandboxFeature.Networking,
        SandboxFeature.VGpu,
        SandboxFeature.AudioInput,
        SandboxFeature.VideoInput,
        SandboxFeature.PrinterRedirection,
        SandboxFeature.ClipboardRedirection,
        SandboxFeature.MappedFolders,
        SandboxFeature.WritableMappedFolders
    ];

    private readonly IProfileRepository _repository;
    private readonly ILogonScriptService? _scripts;
    private readonly ISandboxLauncher _launcher;
    private readonly IHostCapabilities _capabilities;
    private readonly IUpdateChecker _updateChecker;
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
    /// Gets or sets a value indicating whether the library contains no profiles at all.
    /// </summary>
    [ObservableProperty]
    public partial bool HasNoProfiles { get; set; }

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
    /// Gets or sets a value indicating whether a newer release was found.
    /// </summary>
    [ObservableProperty]
    public partial bool UpdateAvailable { get; set; }

    /// <summary>
    /// Gets or sets the localized update banner text.
    /// </summary>
    [ObservableProperty]
    public partial string? UpdateMessage { get; set; }

    /// <summary>
    /// Gets or sets the release page to open from the update banner.
    /// </summary>
    public Uri? UpdateReleaseUri { get; private set; }

    /// <summary>
    /// Initializes a new instance of <see cref="MainViewModel"/>.
    /// </summary>
    public MainViewModel(
        IProfileRepository repository,
        ISandboxLauncher launcher,
        IHostCapabilities capabilities,
        IUpdateChecker updateChecker,
        ILogonScriptService? scripts = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        _capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
        _updateChecker = updateChecker ?? throw new ArgumentNullException(nameof(updateChecker));
        _scripts = scripts;

        var status = capabilities.SandboxStatus;
        IsSandboxInstalled = status == HostSandboxStatus.Available;
        var hasPolicyRestriction = status == HostSandboxStatus.Available && PolicyGatedFeatures
            .Any(feature => _capabilities.GetAvailability(feature).Status == FeatureStatus.DisabledByPolicy);
        EngineState = status switch
        {
            HostSandboxStatus.Available when hasPolicyRestriction => SandboxEngineState.Degraded,
            HostSandboxStatus.Available => SandboxEngineState.Ready,
            _ => SandboxEngineState.Unavailable
        };
        SandboxStatusMessage = status switch
        {
            HostSandboxStatus.Available when hasPolicyRestriction => Localizer.Get("SandboxStatusRestricted"),
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
    /// Gets the engine status-light state shown in the status bar: ready,
    /// degraded (usable but policy-restricted), or unavailable.
    /// </summary>
    public SandboxEngineState EngineState { get; }

    /// <summary>Gets a value indicating whether the engine is fully usable.</summary>
    public bool EngineIsReady => EngineState == SandboxEngineState.Ready;

    /// <summary>Gets a value indicating whether the engine runs with policy restrictions.</summary>
    public bool EngineIsDegraded => EngineState == SandboxEngineState.Degraded;

    /// <summary>Gets a value indicating whether the engine cannot run on this host.</summary>
    public bool EngineIsUnavailable => EngineState == SandboxEngineState.Unavailable;

    /// <summary>
    /// Checks the release feed for a newer version. Runs silently — only an
    /// available update surfaces a banner; failures are swallowed to avoid
    /// interrupting the dashboard for a best-effort check.
    /// </summary>
    [RelayCommand]
    public async Task CheckForUpdatesAsync()
    {
        try
        {
            var result = await _updateChecker.CheckAsync();
            if (result.Status == UpdateCheckStatus.Available &&
                result.LatestVersion is { } latest &&
                result.ReleaseUri is { } uri)
            {
                UpdateReleaseUri = uri;
                // Prefer the raw tag — it carries prerelease/suffix detail the
                // parsed Version drops (e.g. 0.2.0-rc1 shown as "0.2.0").
                UpdateMessage = Localizer.Format("UpdateAvailableMessage", result.LatestTag ?? latest.ToString());
                UpdateAvailable = true;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Best-effort only; never surface update-check failures to the dashboard.
        }
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
            var items = await _repository.GetAllAsync();

            // Move script-backed profiles off the shared-directory layout before
            // the sweep, so their legacy scripts are not mistaken for orphans.
            if (_scripts is not null)
            {
                foreach (var profile in items)
                {
                    try
                    {
                        if (_scripts.MigrateLegacyLayout(profile.Configuration, profile.Id))
                        {
                            await _repository.SaveAsync(profile);
                        }
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        // Best-effort; the profile keeps its legacy layout and is retried next load.
                    }
                }
            }

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
    /// Builds an unsaved draft profile with a default name for the "New Profile"
    /// editor flow; the draft only persists when the user saves it in the editor.
    /// </summary>
    public SandboxProfile CreateDraftProfile()
    {
        // Count-based numbering repeats after deletions; take the first free number.
        var number = 1;
        while (_allProfiles.Any(profile => string.Equals(
                   profile.Name,
                   Localizer.Format("NewProfileDefaultName", number),
                   StringComparison.OrdinalIgnoreCase)))
        {
            number++;
        }

        return new SandboxProfile
        {
            Name = Localizer.Format("NewProfileDefaultName", number),
            Description = Localizer.Get("NewProfileDefaultDescription")
        };
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
            StatusMessage = Localizer.Format("StatusLaunchFailed", Localizer.Core(result.ErrorKey, result.ErrorArgs, result.ErrorMessage ?? Localizer.Get("CoreLauncherFailed")));
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

        // An empty library shows the create-first-profile state even when the
        // user typed a filter — "no matches" only makes sense with profiles to match.
        HasNoProfiles = _allProfiles.Count == 0;
        HasNoMatchingProfiles = _allProfiles.Count > 0 && Profiles.Count == 0;
        if (requiredTags.Count > 0)
        {
            StatusMessage = Localizer.Format("StatusTagFilterMatch", Profiles.Count, _allProfiles.Count);
        }
    }
}
