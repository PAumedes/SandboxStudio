using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SandboxStudio.Core.Models;
using SandboxStudio.Core.Serialization;
using SandboxStudio.Core.Validation;
using SandboxStudio.UI.ViewModels;
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
        SandboxEngineStatus.Text = ViewModel.IsSandboxInstalled
            ? "Windows Sandbox Engine Ready"
            : "Windows Sandbox Engine Unavailable";

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
        if (sender is Button { Tag: SandboxProfile profile })
        {
            try
            {
                await ViewModel.LaunchProfileAsync(profile);
            }
            catch (Exception ex)
            {
                ViewModel.StatusMessage = $"Launch error: {ex.Message}";
            }
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
            await ShowEditDialogAsync(profile);
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = $"Edit error: {ex.Message}";
        }
    }

    private async Task ShowEditDialogAsync(SandboxProfile profile)
    {
        var editViewModel = App.Host.Services.GetRequiredService<EditProfileViewModel>();
        editViewModel.SetProfile(profile);

        var nameTextBox = new TextBox
        {
            Header = "Name",
            Text = editViewModel.Profile.Name,
            MaxLength = 120
        };
        var descriptionTextBox = new TextBox
        {
            Header = "Description",
            Text = editViewModel.Profile.Description,
            MaxLength = 500,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 72
        };
        var networkingComboBox = CreateTriStateComboBox(
            "Networking",
            editViewModel.Profile.Configuration.Networking);
        var vGpuComboBox = CreateTriStateComboBox(
            "vGPU",
            editViewModel.Profile.Configuration.VGpu);
        var memoryTextBox = new TextBox
        {
            Header = "Memory (MB)",
            PlaceholderText = "Optional",
            Text = editViewModel.Profile.Configuration.MemoryInMB?.ToString(CultureInfo.InvariantCulture) ?? string.Empty
        };
        var errorTextBlock = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed
        };

        var dialogContent = new StackPanel { Spacing = 12 };
        dialogContent.Children.Add(errorTextBlock);
        dialogContent.Children.Add(nameTextBox);
        dialogContent.Children.Add(descriptionTextBox);
        dialogContent.Children.Add(networkingComboBox);
        dialogContent.Children.Add(vGpuComboBox);
        dialogContent.Children.Add(memoryTextBox);

        var dialog = new ContentDialog
        {
            Title = "Edit profile",
            Content = dialogContent,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        dialog.PrimaryButtonClick += async (_, args) =>
        {
            var deferral = args.GetDeferral();
            try
            {
                errorTextBlock.Text = string.Empty;
                errorTextBlock.Visibility = Visibility.Collapsed;

                int? memoryInMb = null;
                var memoryText = memoryTextBox.Text.Trim();
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

                var draft = editViewModel.Profile;
                draft.Name = nameTextBox.Text;
                draft.Description = descriptionTextBox.Text;
                draft.Configuration.Networking = GetTriStateValue(networkingComboBox);
                draft.Configuration.VGpu = GetTriStateValue(vGpuComboBox);
                draft.Configuration.MemoryInMB = memoryInMb;

                if (!await editViewModel.SaveAsync())
                {
                    ShowDialogError(editViewModel.ValidationSummary ?? "Please correct the profile values.");
                    args.Cancel = true;
                    return;
                }

                await ViewModel.LoadProfilesAsync();
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
        };

        void ShowDialogError(string message)
        {
            errorTextBlock.Text = message;
            errorTextBlock.Visibility = Visibility.Visible;
        }

        await dialog.ShowAsync();
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

    private static ComboBox CreateTriStateComboBox(string header, string? selectedValue)
    {
        var comboBox = new ComboBox
        {
            Header = header,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        comboBox.Items.Add(new ComboBoxItem { Content = "Unspecified" });
        comboBox.Items.Add(new ComboBoxItem { Content = "Default", Tag = "Default" });
        comboBox.Items.Add(new ComboBoxItem { Content = "Enable", Tag = "Enable" });
        comboBox.Items.Add(new ComboBoxItem { Content = "Disable", Tag = "Disable" });

        var selectedIndex = selectedValue switch
        {
            null => 0,
            "Default" => 1,
            "Enable" => 2,
            "Disable" => 3,
            _ => -1
        };

        if (selectedIndex >= 0)
        {
            comboBox.SelectedIndex = selectedIndex;
        }
        else
        {
            var existingValue = new ComboBoxItem { Content = selectedValue, Tag = selectedValue };
            comboBox.Items.Add(existingValue);
            comboBox.SelectedItem = existingValue;
        }

        return comboBox;
    }

    private static string? GetTriStateValue(ComboBox comboBox) =>
        (comboBox.SelectedItem as ComboBoxItem)?.Tag as string;

    private static string FormatValidationErrors(ValidationResult result) =>
        string.Join(
            "; ",
            result.Messages
                .Where(message => message.Severity == ValidationSeverity.Error)
                .Select(message => $"{message.PropertyName}: {message.Message}"));
}
