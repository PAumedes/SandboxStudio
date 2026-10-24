using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SandboxStudio.Core.Models;
using SandboxStudio.Core.Scripts;
using SandboxStudio.Core.Serialization;
using SandboxStudio.Core.Sessions;
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

        // The custom title bar must track the OS theme for legible caption buttons.
        // Title-bar color customization is honored on Windows 11 only; on Windows 10
        // the setters are documented no-ops and the system-drawn defaults apply.
        if (Content is FrameworkElement rootElement)
        {
            rootElement.ActualThemeChanged += (_, _) => ApplyTitleBarCaptionColors(rootElement);
        }

        ApplyTitleBarCaptionColors(Content as FrameworkElement);

        // Reflect the stored language override in the picker; a stored tag outside
        // the shipped languages shows System default (the override is inert).
        var currentTag = App.Host.Services.GetRequiredService<IAppSettingsStore>().Load().LanguageTag;
        LanguageEnglishItem.IsChecked = string.Equals(currentTag, "en-US", StringComparison.OrdinalIgnoreCase);
        LanguageSpanishItem.IsChecked = string.Equals(currentTag, "es-ES", StringComparison.OrdinalIgnoreCase);
        LanguageSystemDefaultItem.IsChecked = !LanguageEnglishItem.IsChecked && !LanguageSpanishItem.IsChecked;

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

        // Auto-load profiles on startup; update check runs silently alongside.
        _ = ViewModel.CheckForUpdatesAsync();
        _ = ViewModel.LoadProfilesAsync();
    }

    /// <summary>
    /// Parameterless constructor for WinUI tooling / fallback resolution.
    /// </summary>
    public MainWindow() : this(App.Host.Services.GetRequiredService<MainViewModel>())
    {
    }

    private void ApplyTitleBarCaptionColors(FrameworkElement? themedElement)
    {
        if (!Microsoft.UI.Windowing.AppWindowTitleBar.IsCustomizationSupported())
        {
            return;
        }

        var isLight = themedElement?.ActualTheme == ElementTheme.Light;
        var foreground = isLight
            ? Windows.UI.Color.FromArgb(0xFF, 0x1B, 0x1B, 0x1B)
            : Windows.UI.Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);
        var hoverBackground = Windows.UI.Color.FromArgb(0x1A, foreground.R, foreground.G, foreground.B);
        var pressedBackground = Windows.UI.Color.FromArgb(0x33, foreground.R, foreground.G, foreground.B);

        var titleBar = AppWindow.TitleBar;
        titleBar.ButtonForegroundColor = foreground;
        titleBar.ButtonHoverForegroundColor = foreground;
        titleBar.ButtonPressedForegroundColor = foreground;
        titleBar.ButtonBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
        titleBar.ButtonInactiveBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
        titleBar.ButtonHoverBackgroundColor = hoverBackground;
        titleBar.ButtonPressedBackgroundColor = pressedBackground;
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
                    Title = Localizer.Get("WritableWarningTitle"),
                    Content = Localizer.Get("WritableWarningMessage"),
                    PrimaryButtonText = Localizer.Get("LaunchAnywayButtonText"),
                    CloseButtonText = Localizer.Get("CancelButtonText"),
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
            ViewModel.StatusMessage = Localizer.Format("StatusLaunchError", ex.Message);
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
                App.Host.Services.GetRequiredService<IWsbValidator>(),
                App.Host.Services.GetRequiredService<ILogonScriptService>())
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
            ViewModel.StatusMessage = Localizer.Format("StatusEditError", ex.Message);
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
                ViewModel.StatusMessage = Localizer.Format("DuplicateNotFound", profile.Name);
                return;
            }

            ViewModel.TrackProfile(duplicate);
            ViewModel.StatusMessage = Localizer.Format("DuplicateSuccess", profile.Name, duplicate.Name);
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = Localizer.Format("StatusDuplicateError", ex.Message);
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
                    Title = Localizer.Get("DeleteDialogTitle"),
                    Content = Localizer.Format("DeleteDialogMessage", profile.Name),
                    PrimaryButtonText = Localizer.Get("DeleteDialogPrimaryButton"),
                    CloseButtonText = Localizer.Get("CancelButtonText"),
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = Content.XamlRoot
                };

                if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                {
                    return;
                }

                await ViewModel.DeleteProfileAsync(profile);
                await App.Host.Services.GetRequiredService<ILogonScriptService>().DeleteAsync(profile.Id);
            }
            catch (Exception ex)
            {
                ViewModel.StatusMessage = Localizer.Format("StatusDeleteError", ex.Message);
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
                ViewModel.StatusMessage = Localizer.Format("ExportBlocked", FormatValidationErrors(validationResult));
                return;
            }

            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = "SandboxProfile"
            };
            picker.FileTypeChoices.Add(Localizer.Get("WsbFileTypeLabel"), new List<string> { ".wsb" });
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
                ViewModel.StatusMessage = Localizer.Format("ExportBlocked", FormatValidationErrors(result));
                return;
            }

            var warningCount = result.Messages.Count(message => message.Severity == ValidationSeverity.Warning);
            ViewModel.StatusMessage = Localizer.Format("ExportSuccess", file.Path, warningCount);
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = Localizer.Format("StatusExportError", ex.Message);
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
                notes.Add(Localizer.Format("ValidationErrorCount", errorCount));
            }

            if (warningCount > 0)
            {
                notes.Add(Localizer.Format("ValidationWarningCount", warningCount));
            }

            ViewModel.StatusMessage = notes.Count == 0
                ? Localizer.Format("ImportSuccess", result.Profile.Name, file.Name)
                : Localizer.Format("ImportSuccessWithNotes", result.Profile.Name, string.Join(", ", notes), FormatValidationMessages(result.Validation));
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = Localizer.Format("StatusImportFailed", ex.Message);
        }
    }

    private void OnViewUpdateClicked(object sender, RoutedEventArgs e)
    {
        // Last gate before ShellExecute — only HTTPS release links open.
        if (ViewModel.UpdateReleaseUri is not { } uri || uri.Scheme != Uri.UriSchemeHttps)
        {
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = Localizer.Format("UpdateViewError", ex.Message);
        }
    }

    private async void OnSessionsClicked(object sender, RoutedEventArgs e)
    {
        var sessions = App.Host.Services.GetRequiredService<ISandboxSessionManager>();
        var dialog = new SessionsDialog(sessions, WinRT.Interop.WindowNative.GetWindowHandle(this))
        {
            XamlRoot = Content.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private void OnLanguageClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioMenuFlyoutItem item)
        {
            return;
        }

        var tag = item.Tag as string;
        try
        {
            var settings = App.Host.Services.GetRequiredService<IAppSettingsStore>().Load();
            if (string.Equals(settings.LanguageTag, tag, StringComparison.OrdinalIgnoreCase) ||
                (settings.LanguageTag is null && string.IsNullOrEmpty(tag)))
            {
                return;
            }

            App.Host.Services.GetRequiredService<IAppSettingsStore>()
                .Save(settings with { LanguageTag = string.IsNullOrEmpty(tag) ? null : tag });
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = Localizer.Format("StatusSettingsSaveError", ex.Message);
            return;
        }

        // The override applies at the next launch; selecting the language the app is
        // already running in cancels the pending restart notice.
        var effectiveTag = Microsoft.Windows.Globalization.ApplicationLanguages.Languages
            .FirstOrDefault();
        LanguageRestartInfoBar.IsOpen =
            !string.Equals(effectiveTag, tag, StringComparison.OrdinalIgnoreCase) &&
            !(effectiveTag is null && string.IsNullOrEmpty(tag));
    }

    private static string FormatValidationMessages(ValidationResult result) =>
        string.Join(
            "; ",
            result.Messages
                .Where(message => message.Severity != ValidationSeverity.Info)
                .Select(message => Localizer.Format("ValidationMessageFormat", message.PropertyName, Localizer.Core(message.MessageKey, message.MessageArgs, message.Message))));

    private static string FormatValidationErrors(ValidationResult result) =>
        string.Join(
            "; ",
            result.Messages
                .Where(message => message.Severity == ValidationSeverity.Error)
                .Select(message => Localizer.Format("ValidationMessageFormat", message.PropertyName, Localizer.Core(message.MessageKey, message.MessageArgs, message.Message))));
}
