using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using SandboxStudio.Core.Capabilities;
using SandboxStudio.Core.Execution;
using SandboxStudio.Core.Presets;
using SandboxStudio.Core.Scripts;
using SandboxStudio.Core.Serialization;
using SandboxStudio.Core.Sessions;
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
        // Must precede InitializeComponent: PrimaryLanguageOverride only affects
        // resource contexts created after it is set. The stored tag is user-editable
        // file content, so ignore values that are malformed or not among the
        // manifest languages instead of letting the setter throw.
        var storedTag = new JsonAppSettingsStore().Load().LanguageTag;
        if (!string.IsNullOrEmpty(storedTag) &&
            Microsoft.Windows.Globalization.ApplicationLanguages.ManifestLanguages
                .Any(language => string.Equals(language, storedTag, StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = storedTag;
            }
            catch (ArgumentException)
            {
                // Malformed tag survived the manifest check; fall back to OS language.
            }
        }

        InitializeComponent();

        Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                // Core Services
                services.AddSingleton<IWsbSerializer, WsbSerializer>();
                services.AddSingleton<IWsbValidator, WsbValidator>();
                services.AddSingleton<WsbExportService>();
                services.AddSingleton<WsbImportService>();
                services.AddSingleton<ProfileDuplicationService>();
                services.AddSingleton<ILogonScriptService, LogonScriptService>();
                services.AddSingleton<IAppSettingsStore, JsonAppSettingsStore>();
                services.AddSingleton<CuratedPresets>();
                services.AddSingleton<IProfileRepository, JsonProfileRepository>();
                services.AddSingleton<ISandboxLauncher, SandboxLauncher>();
                services.AddSingleton<IHostCapabilities, HostCapabilities>();
                services.AddSingleton<ISandboxSessionManager, WsbSessionManager>();

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
