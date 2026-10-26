using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
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

        var storedSettings = App.Services.GetRequiredService<IAppSettingsStore>().Load();
        if (Content is FrameworkElement rootElement)
        {
            rootElement.RequestedTheme = AppTheme.FromSetting(storedSettings.Theme);

            // The custom title bar must track the OS theme for legible caption buttons.
            // Title-bar color customization is honored on Windows 11 only; on Windows 10
            // the setters are documented no-ops and the system-drawn defaults apply.
            rootElement.ActualThemeChanged += (_, _) => ApplyTitleBarCaptionColors(rootElement);
        }

        ApplyTitleBarCaptionColors(Content as FrameworkElement);

        foreach (var infoBar in new[] { SandboxStatusInfoBar, LanguageRestartInfoBar, UpdateInfoBar, NoMatchingProfilesInfoBar })
        {
            CollapseWhenClosed(infoBar);
        }

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

        // Auto-load profiles on startup; update check runs silently alongside.
        // Store installs are updated by the Store, so only the portable build asks GitHub.
        if (!AppDistribution.IsPackaged)
        {
            _ = ViewModel.CheckForUpdatesAsync();
        }

        _initialLoad = ViewModel.LoadProfilesAsync();

        Activated += OnFirstActivated;
    }

    private readonly Task _initialLoad;

    /// <summary>
    /// A closed InfoBar keeps its slot in the banner StackPanel, and with it the
    /// Spacing gap above the profile cards, so it is collapsed while closed.
    /// </summary>
    private static void CollapseWhenClosed(InfoBar infoBar)
    {
        static void Sync(InfoBar bar) =>
            bar.Visibility = bar.IsOpen ? Visibility.Visible : Visibility.Collapsed;

        Sync(infoBar);
        infoBar.RegisterPropertyChangedCallback(InfoBar.IsOpenProperty, (sender, _) => Sync((InfoBar)sender));
    }

    /// <summary>
    /// Parameterless constructor for WinUI tooling / fallback resolution.
    /// </summary>
    public MainWindow() : this(App.Services.GetRequiredService<MainViewModel>())
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

    private void OnFirstActivated(object sender, WindowActivatedEventArgs args)
    {
        // The quick-start guide needs a live XamlRoot. Activation can precede the
        // root's Loaded event, so wait for it when the tree is not loaded yet.
        Activated -= OnFirstActivated;
        if (Content is FrameworkElement { IsLoaded: false } root)
        {
            void OnRootLoaded(object loadedSender, RoutedEventArgs loadedArgs)
            {
                root.Loaded -= OnRootLoaded;
                _ = ShowQuickStartIfNeededAsync();
            }

            root.Loaded += OnRootLoaded;
            return;
        }

        _ = ShowQuickStartIfNeededAsync();
    }

    private async Task ShowQuickStartIfNeededAsync()
    {
        // Started fire-and-forget, so failures are reported here rather than lost.
        try
        {
            var settingsStore = App.Services.GetRequiredService<IAppSettingsStore>();
            if (settingsStore.Load().QuickStartDismissed)
            {
                return;
            }

            await ShowQuickStartAsync();
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = Localizer.Format("StatusQuickStartError", ex.Message);
        }
    }

    /// <summary>
    /// Theme for dialogs: they are hosted in the popup layer and do not inherit
    /// the window root's <see cref="FrameworkElement.RequestedTheme"/>.
    /// </summary>
    private ElementTheme DialogTheme =>
        Content is FrameworkElement root ? root.RequestedTheme : ElementTheme.Default;

    private async Task ShowQuickStartAsync()
    {
        var dialog = new QuickStartDialog { XamlRoot = Content.XamlRoot, RequestedTheme = DialogTheme };
        await dialog.ShowAsync();

        if (!dialog.DontShowAgain)
        {
            return;
        }

        var settingsStore = App.Services.GetRequiredService<IAppSettingsStore>();
        try
        {
            settingsStore.Save(settingsStore.Load() with { QuickStartDismissed = true });
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = Localizer.Format("StatusSettingsSaveError", ex.Message);
        }
    }

    private async void OnSettingsClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new SettingsDialog(App.Services.GetRequiredService<IAppSettingsStore>())
            {
                XamlRoot = Content.XamlRoot,
                RequestedTheme = DialogTheme
            };

            await dialog.ShowAsync();

            LanguageRestartInfoBar.IsOpen = dialog.LanguageRestartRequired;

            if (dialog.QuickStartRequested)
            {
                await ShowQuickStartAsync();
            }
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = Localizer.Format("StatusSettingsError", ex.Message);
        }
    }

    private async void OnNewProfileClicked(object sender, RoutedEventArgs e) =>
        await OpenEditorAsync(ViewModel.CreateDraftProfile());

    private void OnCardDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement card || card.DataContext is not SandboxProfile profile)
        {
            return;
        }

        // Double-tapping the Launch or "..." button must not open the editor.
        if (IsWithinButton(e.OriginalSource as DependencyObject))
        {
            return;
        }

        e.Handled = true;
        _ = OpenEditorAsync(profile);
    }

    private void OnProfileMoreClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement button)
        {
            return;
        }

        // The card's context menu is attached to its root so right-click works
        // anywhere on the card; the "..." button surfaces that same flyout.
        for (DependencyObject? element = button; element is not null; element = VisualTreeHelper.GetParent(element))
        {
            if (element is UIElement { ContextFlyout: { } flyout })
            {
                flyout.ShowAt(button);
                return;
            }
        }
    }

    private static bool IsWithinButton(DependencyObject? element)
    {
        for (; element is not null; element = VisualTreeHelper.GetParent(element))
        {
            if (element is ButtonBase)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Opens the profile editor for an existing profile or an unsaved draft.
    /// </summary>
    private async Task OpenEditorAsync(SandboxProfile profile)
    {
        try
        {
            var editViewModel = App.Services.GetRequiredService<EditProfileViewModel>();
            editViewModel.SetProfile(profile);

            var dialog = new EditProfileDialog(
                editViewModel,
                App.Services.GetRequiredService<IWsbValidator>(),
                App.Services.GetRequiredService<ILogonScriptService>())
            {
                XamlRoot = Content.XamlRoot,
                RequestedTheme = DialogTheme
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

    private bool _launchInProgress;

    private async void OnLaunchClicked(object sender, RoutedEventArgs e)
    {
        // Click fires per press — a fast double-click must not launch twice or
        // race two writable-mapping warning dialogs.
        if (_launchInProgress || sender is not Button { Tag: SandboxProfile profile })
        {
            return;
        }

        _launchInProgress = true;
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
                    XamlRoot = Content.XamlRoot,
                    RequestedTheme = DialogTheme
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
        finally
        {
            _launchInProgress = false;
        }
    }

    private async void OnEditClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: SandboxProfile profile })
        {
            return;
        }

        await OpenEditorAsync(profile);
    }

    private async void OnDuplicateClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: SandboxProfile profile })
        {
            return;
        }

        try
        {
            var duplicationService = App.Services.GetRequiredService<ProfileDuplicationService>();
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
                    XamlRoot = Content.XamlRoot,
                    RequestedTheme = DialogTheme
                };

                if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                {
                    return;
                }

                await ViewModel.DeleteProfileAsync(profile);
                await App.Services.GetRequiredService<ILogonScriptService>().DeleteAsync(profile.Id);
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
            var validator = App.Services.GetRequiredService<IWsbValidator>();
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

            var exportService = App.Services.GetRequiredService<WsbExportService>();
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
            if (file is not null)
            {
                await ImportFileAsync(file.Path);
            }
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = Localizer.Format("StatusImportFailed", ex.Message);
        }
    }

    /// <summary>
    /// Imports a <c>.wsb</c> file the app was opened with (Explorer "Open with",
    /// or a path on the command line), once the startup profile load completes.
    /// </summary>
    public async Task ImportOnLaunchAsync(string path)
    {
        await _initialLoad;
        await ImportFileAsync(path);
    }

    private async Task ImportFileAsync(string path)
    {
        try
        {
            var importService = App.Services.GetRequiredService<WsbImportService>();
            var result = await importService.ImportAsync(path);
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
                ? Localizer.Format("ImportSuccess", result.Profile.Name, Path.GetFileName(path))
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
        try
        {
            var sessions = App.Services.GetRequiredService<ISandboxSessionManager>();
            var dialog = new SessionsDialog(sessions, WinRT.Interop.WindowNative.GetWindowHandle(this))
            {
                XamlRoot = Content.XamlRoot,
                RequestedTheme = DialogTheme
            };
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = Localizer.Format("StatusSessionsError", ex.Message);
        }
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
