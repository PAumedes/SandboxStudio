using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SandboxStudio.Core.Models;
using SandboxStudio.Core.Validation;
using SandboxStudio.UI.ViewModels;

namespace SandboxStudio.UI.Views;

/// <summary>
/// Dialog for editing a profile's name, description, sandbox settings, startup command, and mapped folders.
/// </summary>
public sealed partial class EditProfileDialog : ContentDialog
{
    private readonly IWsbValidator _validator;

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
    public EditProfileDialog(EditProfileViewModel viewModel, IWsbValidator validator)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));

        InitializeComponent();

        var draft = ViewModel.Profile;
        NameTextBox.Text = draft.Name;
        DescriptionTextBox.Text = draft.Description;
        MemoryTextBox.Text = draft.Configuration.MemoryInMB?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        StartupCommandTextBox.Text = draft.Configuration.LogonCommand?.Command ?? string.Empty;

        UpdateWritableMappingControls();
    }

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
    }

    private void OnRemoveMappedFolderClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: WsbMappedFolder folder })
        {
            ViewModel.RemoveMappedFolder(folder);
            UpdateWritableMappingControls();
        }
    }

    private void OnAddMappedFolderClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.AddMappedFolder();
        UpdateWritableMappingControls();
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
            draft.Configuration.MemoryInMB = memoryInMb;
            var startupCommand = StartupCommandTextBox.Text;
            draft.Configuration.LogonCommand = string.IsNullOrWhiteSpace(startupCommand)
                ? null
                : new WsbLogonCommand { Command = startupCommand };

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

            var warningMessages = validationResult.Messages
                .Where(message => message.Severity == ValidationSeverity.Warning)
                .Select(message => $"{message.PropertyName}: {message.Message}")
                .ToList();
            SavedSuccessfully = true;
            PostSaveStatusMessage = warningMessages.Count == 0
                ? $"Saved '{draft.Name}'."
                : $"Saved '{draft.Name}' with {warningMessages.Count} warning(s): {string.Join("; ", warningMessages)}";
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
