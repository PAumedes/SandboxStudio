using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SandboxStudio.Core.Models;
using SandboxStudio.Core.Serialization;
using SandboxStudio.Core.Storage;
using SandboxStudio.Core.Validation;
using SandboxStudio.UI.ViewModels;
using SandboxStudio.UI.Views;
using Windows.Storage.Pickers;

namespace SandboxStudio.UI;

/// <summary>
/// Main window displaying profile cards and orchestration controls.
/// </summary>
public sealed partial class MainWindow : Window
{
    /// <summary>
    /// Gets the governing ViewModel.
    /// </summary>
    public MainViewModel ViewModel { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="MainWindow"/> with injected dependencies.
    /// </summary>
    public MainWindow(MainViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarDragRegion);
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "SandboxStudio.ico"));

        // Check Sandbox availability
        SandboxStatusInfoBar.IsOpen = !ViewModel.IsSandboxInstalled;
        if (ViewModel.SandboxUnavailableReason is { } unavailableReason)
        {
            SandboxStatusInfoBar.Message = unavailableReason;
        }

        if (ViewModel.SandboxUnavailableTitle is { } unavailableTitle)
        {
            SandboxStatusInfoBar.Title = unavailableTitle;
        }

        SandboxEngineStatus.Text = ViewModel.SandboxStatusMessage ?? string.Empty;

        // Auto-load profiles on startup
        _ = ViewModel.LoadProfilesAsync();
    }

    /// <summary>
    /// Parameterless constructor for WinUI tooling / fallback resolution.
    /// </summary>
    public MainWindow() : this(App.Host.Services.GetRequiredService<MainViewModel>())
    {
    }

    private async void OnLaunchClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: SandboxProfile profile })
        {
            return;
        }

        try
        {
            if (profile.Configuration.MappedFolders.Any(folder => !folder.IsReadOnly))
            {
                var dialog = new ContentDialog
                {
                    Title = "Writable host folder access",
                    Content = "This profile allows Windows Sandbox to change files in mapped host folders. Continue only if you trust this configuration.",
                    PrimaryButtonText = "Launch anyway",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = Content.XamlRoot
                };

                if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                {
                    return;
                }
            }

            await ViewModel.LaunchProfileAsync(profile);
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = $"Launch error: {ex.Message}";
        }
    }

    private async void OnEditClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: SandboxProfile profile })
        {
            return;
        }

        try
        {
            var editViewModel = App.Host.Services.GetRequiredService<EditProfileViewModel>();
            editViewModel.SetProfile(profile);

            var dialog = new EditProfileDialog(
                editViewModel,
                App.Host.Services.GetRequiredService<IWsbValidator>())
            {
                XamlRoot = Content.XamlRoot
            };

            await dialog.ShowAsync();
            if (dialog.SavedSuccessfully)
            {
                await ViewModel.LoadProfilesAsync();
                ViewModel.StatusMessage = dialog.PostSaveStatusMessage;
            }
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = $"Edit error: {ex.Message}";
        }
    }

    private async void OnDuplicateClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: SandboxProfile profile })
        {
            return;
        }

        try
        {
            var duplicationService = App.Host.Services.GetRequiredService<ProfileDuplicationService>();
            var duplicate = await duplicationService.DuplicateAsync(profile.Id);
            if (duplicate is null)
            {
                ViewModel.StatusMessage = $"Could not duplicate '{profile.Name}': profile no longer exists.";
                return;
            }

            ViewModel.TrackProfile(duplicate);
            ViewModel.StatusMessage = $"Duplicated '{profile.Name}' as '{duplicate.Name}'.";
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = $"Duplicate error: {ex.Message}";
        }
    }

    private async void OnDeleteClicked(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { Tag: SandboxProfile profile })
        {
            try
            {
                var dialog = new ContentDialog
                {
                    Title = "Delete profile?",
                    Content = $"Delete '{profile.Name}'? This action cannot be undone.",
                    PrimaryButtonText = "Delete",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = Content.XamlRoot
                };

                if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                {
                    return;
                }

                await ViewModel.DeleteProfileAsync(profile);
            }
            catch (Exception ex)
            {
                ViewModel.StatusMessage = $"Delete error: {ex.Message}";
            }
        }
    }

    private async void OnExportClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: SandboxProfile profile })
        {
            return;
        }

        try
        {
            var validator = App.Host.Services.GetRequiredService<IWsbValidator>();
            var validationResult = validator.Validate(profile.Configuration);
            if (!validationResult.IsValid)
            {
                ViewModel.StatusMessage = $"Export blocked: {FormatValidationErrors(validationResult)}";
                return;
            }

            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = "SandboxProfile"
            };
            picker.FileTypeChoices.Add("Windows Sandbox configuration", new List<string> { ".wsb" });
            WinRT.Interop.InitializeWithWindow.Initialize(
                picker,
                WinRT.Interop.WindowNative.GetWindowHandle(this));

            var file = await picker.PickSaveFileAsync();
            if (file is null)
            {
                return;
            }

            var exportService = App.Host.Services.GetRequiredService<WsbExportService>();
            var result = await exportService.ExportAsync(profile.Configuration, file.Path);
            if (!result.IsValid)
            {
                ViewModel.StatusMessage = $"Export blocked: {FormatValidationErrors(result)}";
                return;
            }

            var warningCount = result.Messages.Count(message => message.Severity == ValidationSeverity.Warning);
            ViewModel.StatusMessage = $"Exported .wsb to {file.Path} (warnings: {warningCount}).";
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = $"Export error: {ex.Message}";
        }
    }

    private async void OnImportClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };
            picker.FileTypeFilter.Add(".wsb");
            WinRT.Interop.InitializeWithWindow.Initialize(
                picker,
                WinRT.Interop.WindowNative.GetWindowHandle(this));

            var file = await picker.PickSingleFileAsync();
            if (file is null)
            {
                return;
            }

            var importService = App.Host.Services.GetRequiredService<WsbImportService>();
            var result = await importService.ImportAsync(file.Path);
            ViewModel.FilterText = string.Empty;
            await ViewModel.LoadProfilesAsync();

            var errorCount = result.Validation.Messages.Count(message => message.Severity == ValidationSeverity.Error);
            var warningCount = result.Validation.Messages.Count(message => message.Severity == ValidationSeverity.Warning);
            var notes = new List<string>();
            if (errorCount > 0)
            {
                notes.Add($"{errorCount} error(s)");
            }

            if (warningCount > 0)
            {
                notes.Add($"{warningCount} warning(s)");
            }

            ViewModel.StatusMessage = notes.Count == 0
                ? $"Imported '{result.Profile.Name}' from {file.Name}."
                : $"Imported '{result.Profile.Name}' with {string.Join(", ", notes)}: {FormatValidationMessages(result.Validation)}";
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = $"Import failed: {ex.Message}";
        }
    }

    private static string FormatValidationMessages(ValidationResult result) =>
        string.Join(
            "; ",
            result.Messages
                .Where(message => message.Severity != ValidationSeverity.Info)
                .Select(message => $"{message.PropertyName}: {message.Message}"));

    private static string FormatValidationErrors(ValidationResult result) =>
        string.Join(
            "; ",
            result.Messages
                .Where(message => message.Severity == ValidationSeverity.Error)
                .Select(message => $"{message.PropertyName}: {message.Message}"));
}
