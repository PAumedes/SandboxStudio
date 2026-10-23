using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SandboxStudio.Core.Capabilities;
using SandboxStudio.Core.Models;
using SandboxStudio.Core.Scripts;
using SandboxStudio.Core.Validation;
using SandboxStudio.UI.ViewModels;

namespace SandboxStudio.UI.Views;

/// <summary>
/// Dialog for editing a profile's name, description, sandbox settings, startup command, and mapped folders.
/// </summary>
public sealed partial class EditProfileDialog : ContentDialog
{
    private readonly IWsbValidator _validator;
    private readonly ILogonScriptService _scriptService;

    /// <summary>
    /// Gets the ViewModel backing the dialog.
    /// </summary>
    public EditProfileViewModel ViewModel { get; }

    /// <summary>
    /// Gets a value indicating whether the profile was saved successfully.
    /// </summary>
    public bool SavedSuccessfully { get; private set; }

    /// <summary>
    /// Gets the status message describing the save outcome, including any validation warnings.
    /// </summary>
    public string? PostSaveStatusMessage { get; private set; }

    /// <summary>
    /// Initializes a new instance of <see cref="EditProfileDialog"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="EditProfileViewModel.SetProfile"/> must be called on <paramref name="viewModel"/>
    /// before constructing the dialog so the draft is populated.
    /// </remarks>
    public EditProfileDialog(EditProfileViewModel viewModel, IWsbValidator validator, ILogonScriptService scriptService)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _scriptService = scriptService ?? throw new ArgumentNullException(nameof(scriptService));

        InitializeComponent();

        var draft = ViewModel.Profile;
        NameTextBox.Text = draft.Name ?? string.Empty;
        DescriptionTextBox.Text = draft.Description ?? string.Empty;
        TagsTextBox.Text = draft.Tags is { } tags ? ProfileTags.Format(tags) : string.Empty;
        MemoryTextBox.Text = draft.Configuration.MemoryInMB?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        if (draft.LogonScriptCommands is { Count: > 0 } scriptCommands)
        {
            ScriptModeCheckBox.IsChecked = true;
            StartupCommandTextBox.Text = string.Join("\r", scriptCommands);
        }
        else
        {
            StartupCommandTextBox.Text = draft.Configuration.LogonCommand?.Command ?? string.Empty;
        }

        UpdateScriptModeControls();

        RefreshPolicyOverrideInfoBar();
        UpdateWritableMappingControls();
        ApplyAvailabilityCaptions();
    }

    private void OnScriptModeToggled(object sender, RoutedEventArgs e) => UpdateScriptModeControls();

    private void UpdateScriptModeControls() =>
        StartupCommandTextBox.AcceptsReturn = ScriptModeCheckBox.IsChecked == true;

    private void ApplyAvailabilityCaptions()
    {
        ApplyAvailabilityCaption(NetworkingAvailabilityText, SandboxFeature.Networking);
        ApplyAvailabilityCaption(VGpuAvailabilityText, SandboxFeature.VGpu);
        ApplyAvailabilityCaption(AudioInputAvailabilityText, SandboxFeature.AudioInput);
        ApplyAvailabilityCaption(VideoInputAvailabilityText, SandboxFeature.VideoInput);
        ApplyAvailabilityCaption(ClipboardAvailabilityText, SandboxFeature.ClipboardRedirection);
        ApplyAvailabilityCaption(PrinterAvailabilityText, SandboxFeature.PrinterRedirection);
        ApplyAvailabilityCaption(ProtectedClientAvailabilityText, SandboxFeature.ProtectedClient);
        ApplyAvailabilityCaption(MemoryAvailabilityText, SandboxFeature.MemoryInMB);
        ApplyAvailabilityCaption(MappedFoldersAvailabilityText, SandboxFeature.MappedFolders, "Mapped folders");
        if (ViewModel.GetFeatureAvailability(SandboxFeature.MappedFolders).Status == FeatureStatus.Available)
        {
            // When all folder mapping is unavailable, the writable-specific caption is redundant.
            ApplyAvailabilityCaption(WritableMappedFoldersAvailabilityText, SandboxFeature.WritableMappedFolders, "Writable mapped folders");
        }

        ApplyAvailabilityCaption(MappedFolderEnvVarsAvailabilityText, SandboxFeature.MappedFolderEnvironmentVariables, "Environment variables in host paths");
    }

    private void ApplyAvailabilityCaption(TextBlock caption, SandboxFeature feature, string? label = null)
    {
        var availability = ViewModel.GetFeatureAvailability(feature);
        if (availability.Status == FeatureStatus.Available)
        {
            return;
        }

        caption.Text = label is null
            ? $"\u26A0 {availability.Reason}"
            : $"\u26A0 {label}: {availability.Reason}";
        caption.Visibility = Visibility.Visible;
    }

    private void RefreshPolicyOverrideInfoBar()
    {
        var policyOverrides = ViewModel.GetPolicyOverrides();
        PolicyOverrideInfoBar.IsOpen = policyOverrides.Count > 0;
        if (policyOverrides.Count > 0)
        {
            PolicyOverrideInfoBar.Message = string.Join(" ", policyOverrides.Select(o => o.Message));
        }
    }

    private void OnTriStateSelectionChanged(object sender, SelectionChangedEventArgs e) =>
        RefreshPolicyOverrideInfoBar();

    private void UpdateWritableMappingControls()
    {
        var hasWritableMapping = ViewModel.Profile.Configuration.MappedFolders.Any(folder => !folder.IsReadOnly);
        WritableMappingWarning.Visibility = hasWritableMapping ? Visibility.Visible : Visibility.Collapsed;
        AcknowledgeWritableMapping.Visibility = hasWritableMapping ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnReadOnlyStateChanged(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { DataContext: WsbMappedFolder folder } checkBox)
        {
            folder.IsReadOnly = checkBox.IsChecked == true;
        }

        UpdateWritableMappingControls();
        RefreshPolicyOverrideInfoBar();
    }

    private void OnRemoveMappedFolderClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: WsbMappedFolder folder })
        {
            ViewModel.RemoveMappedFolder(folder);
            UpdateWritableMappingControls();
            RefreshPolicyOverrideInfoBar();
        }
    }

    private void OnAddMappedFolderClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.AddMappedFolder();
        UpdateWritableMappingControls();
        RefreshPolicyOverrideInfoBar();
    }

    private void ShowDialogError(string message)
    {
        ErrorTextBlock.Text = message;
        ErrorTextBlock.Visibility = Visibility.Visible;
        DialogScrollViewer.UpdateLayout();
        DialogScrollViewer.ChangeView(null, 0, null, disableAnimation: true);
    }

    private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            ErrorTextBlock.Text = string.Empty;
            ErrorTextBlock.Visibility = Visibility.Collapsed;

            int? memoryInMb = null;
            var memoryText = MemoryTextBox.Text.Trim();
            if (memoryText.Length > 0)
            {
                if (!int.TryParse(
                        memoryText,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var parsedMemory))
                {
                    ShowDialogError("Memory must be a whole number of megabytes.");
                    args.Cancel = true;
                    return;
                }

                memoryInMb = parsedMemory;
            }

            var draft = ViewModel.Profile;
            draft.Name = NameTextBox.Text;
            draft.Description = DescriptionTextBox.Text;
            draft.Tags = ProfileTags.Parse(TagsTextBox.Text);
            draft.Configuration.MemoryInMB = memoryInMb;
            // WinUI text boxes return '\r' line separators, not '\n'.
            var lines = StartupCommandTextBox.Text
                .Split('\r', '\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .ToList();
            var scriptCommands = ScriptModeCheckBox.IsChecked == true ? lines : null;
            var wasScriptBacked = _scriptService.IsScriptBacked(draft.Configuration, draft.Id);
            var previousCommand = draft.Configuration.LogonCommand?.Command;

            if (scriptCommands is { Count: > 0 })
            {
                await _scriptService.ApplyAsync(draft.Configuration, draft.Id, scriptCommands);
                draft.LogonScriptCommands = scriptCommands;

                // Surface the managed mount in the folder list; ApplyAsync repairs
                // it if the row was edited or removed.
                var managedFolder = draft.Configuration.MappedFolders.FirstOrDefault(_scriptService.IsManagedMapping);
                if (managedFolder is not null && !ViewModel.MappedFolders.Contains(managedFolder))
                {
                    ViewModel.MappedFolders.Add(managedFolder);
                }
            }
            else
            {
                // Releasing script mode (or an empty box) removes managed script
                // artifacts; the first non-empty line supplies a plain logon
                // command — unless it is the packaged command we just tore down.
                await _scriptService.UnapplyAsync(draft.Configuration, draft.Id);
                draft.LogonScriptCommands = null;
                var startupCommand = lines.FirstOrDefault();
                var resurrectingTornDownScript = wasScriptBacked &&
                    string.Equals(startupCommand, previousCommand, StringComparison.Ordinal);
                draft.Configuration.LogonCommand = startupCommand is null || resurrectingTornDownScript
                    ? null
                    : new WsbLogonCommand { Command = startupCommand };
            }

            if (draft.Configuration.MappedFolders.Any(folder => !folder.IsReadOnly) &&
                AcknowledgeWritableMapping.IsChecked != true)
            {
                ShowDialogError("Acknowledge that writable mappings let the sandbox change host files before saving.");
                args.Cancel = true;
                return;
            }

            var validationResult = _validator.Validate(draft.Configuration);

            if (!await ViewModel.SaveAsync())
            {
                ShowDialogError(ViewModel.ValidationSummary ?? "Please correct the profile values.");
                args.Cancel = true;
                return;
            }

            // The save landed — now it is safe to drop the script file when the
            // profile no longer uses script mode.
            if (scriptCommands is not { Count: > 0 })
            {
                await _scriptService.DeleteAsync(draft.Id);
            }

            var warningMessages = validationResult.Messages
                .Where(message => message.Severity == ValidationSeverity.Warning)
                .Select(message => $"{message.PropertyName}: {message.Message}")
                .ToList();
            var overrideMessages = ViewModel.GetPolicyOverrides()
                .Select(policyOverride => policyOverride.Message)
                .ToList();
            var notes = new List<string>();
            if (warningMessages.Count > 0)
            {
                notes.Add($"{warningMessages.Count} warning(s): {string.Join("; ", warningMessages)}");
            }

            if (overrideMessages.Count > 0)
            {
                notes.Add($"host policy overrides: {string.Join("; ", overrideMessages)}");
            }

            if (scriptCommands is { Count: > 0 } && _scriptService.HasNonAsciiContent(scriptCommands))
            {
                notes.Add("the script contains non-ASCII characters that cmd.exe may decode incorrectly");
            }

            SavedSuccessfully = true;
            PostSaveStatusMessage = notes.Count == 0
                ? $"Saved '{draft.Name}'."
                : $"Saved '{draft.Name}' with {string.Join("; ", notes)}.";
        }
        catch (Exception ex)
        {
            ShowDialogError($"The profile could not be saved: {ex.Message}");
            args.Cancel = true;
        }
        finally
        {
            deferral.Complete();
        }
    }
}
