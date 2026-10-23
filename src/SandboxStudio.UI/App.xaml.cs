using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using SandboxStudio.Core.Execution;
using SandboxStudio.Core.Serialization;
using SandboxStudio.Core.Storage;
using SandboxStudio.Core.Validation;
using SandboxStudio.UI.ViewModels;

namespace SandboxStudio.UI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public sealed partial class App : Application
{
    private Window? _mainWindow;

    /// <summary>
    /// Gets the current application host providing dependency injection.
    /// </summary>
    public static IHost Host { get; private set; } = null!;

    /// <summary>
    /// Initializes the singleton application object.
    /// </summary>
    public App()
    {
        InitializeComponent();

        Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                // Core Services
                services.AddSingleton<IWsbSerializer, WsbSerializer>();
                services.AddSingleton<IWsbValidator, WsbValidator>();
                services.AddSingleton<WsbExportService>();
                services.AddSingleton<IProfileRepository, JsonProfileRepository>();
                services.AddSingleton<ISandboxLauncher, SandboxLauncher>();

                // ViewModels
                services.AddTransient<MainViewModel>();
                services.AddTransient<EditProfileViewModel>();

                // Views
                services.AddSingleton<MainWindow>();
            })
            .Build();
    }

    /// <summary>
    /// Invoked when the application is launched normally by the end user.
    /// </summary>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _mainWindow = Host.Services.GetRequiredService<MainWindow>();
        _mainWindow.Closed += (_, _) => Host.Dispose();
        _mainWindow.Activate();
    }
}
