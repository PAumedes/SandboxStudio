using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SandboxStudio.Core.Models;
using SandboxStudio.UI.ViewModels;

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

        // Check Sandbox availability
        SandboxStatusInfoBar.IsOpen = !ViewModel.IsSandboxInstalled;

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

    private async void OnDeleteClicked(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { Tag: SandboxProfile profile })
        {
            try
            {
                await ViewModel.DeleteProfileAsync(profile);
            }
            catch (Exception ex)
            {
                ViewModel.StatusMessage = $"Delete error: {ex.Message}";
            }
        }
    }
}
