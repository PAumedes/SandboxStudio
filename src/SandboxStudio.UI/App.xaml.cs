using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using SandboxStudio.Core.Capabilities;
using SandboxStudio.Core.Execution;
using SandboxStudio.Core.Scripts;
using SandboxStudio.Core.Serialization;
using SandboxStudio.Core.Sessions;
using SandboxStudio.Core.Storage;
using SandboxStudio.Core.Updates;
using SandboxStudio.Core.Validation;
using SandboxStudio.UI.ViewModels;

namespace SandboxStudio.UI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public sealed partial class App : Application
{
    private Window? _mainWindow;
    private static ServiceProvider? _serviceProvider;

    /// <summary>
    /// Gets the application's dependency-injection container.
    /// </summary>
    public static IServiceProvider Services => _serviceProvider
        ?? throw new InvalidOperationException("Services are not available before the App is constructed.");

    /// <summary>
    /// Initializes the singleton application object.
    /// </summary>
    public App()
    {
        // Must precede InitializeComponent: PrimaryLanguageOverride only affects
        // resource contexts created after it is set. The stored tag is user-editable
        // file content, so ignore values that are malformed or not among the
        // supported languages instead of letting the setter throw.
        // (ManifestLanguages cannot gate this: it is empty for unpackaged apps.)
        var storedTag = new JsonAppSettingsStore().Load().LanguageTag;
        if (Localizer.IsSupportedLanguage(storedTag))
        {
            try
            {
                Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = storedTag;
            }
            catch (Exception)
            {
                // Malformed tag survived the supported-language check; fall back to OS language.
            }
        }
        else if (AppDistribution.IsPackaged)
        {
            // On packaged builds the WASDK setter delegates to the inbox API and
            // that override persists between sessions; clear a stale one so
            // "System default" actually takes effect after a restart.
            try
            {
                Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = string.Empty;
            }
            catch (Exception)
            {
                // No persisted override to clear; nothing to do.
            }
        }

        InitializeComponent();

        // A plain container: the app runs no hosted services, so the generic host's
        // configuration and logging stack would only add assemblies and startup work.
        var services = new ServiceCollection();

        // Core Services
        services.AddSingleton<IWsbSerializer, WsbSerializer>();
        services.AddSingleton<IWsbValidator, WsbValidator>();
        services.AddSingleton<WsbExportService>();
        services.AddSingleton<WsbImportService>();
        services.AddSingleton<ProfileDuplicationService>();
        services.AddSingleton<ILogonScriptService, LogonScriptService>();
        services.AddSingleton<IAppSettingsStore, JsonAppSettingsStore>();
        services.AddSingleton<IProfileRepository, JsonProfileRepository>();
        services.AddSingleton<ISandboxLauncher, SandboxLauncher>();
        services.AddSingleton<IHostCapabilities, HostCapabilities>();
        services.AddSingleton<ISandboxSessionManager, WsbSessionManager>();
        services.AddSingleton<IUpdateChecker, GitHubUpdateChecker>();

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<EditProfileViewModel>();

        // Views
        services.AddSingleton<MainWindow>();

        _serviceProvider = services.BuildServiceProvider();
    }

    /// <summary>
    /// Invoked when the application is launched by the end user, including to
    /// open a <c>.wsb</c> file, which is imported as a new profile.
    /// </summary>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var mainWindow = Services.GetRequiredService<MainWindow>();
        _mainWindow = mainWindow;
        _mainWindow.Closed += (_, _) => _serviceProvider?.Dispose();
        _mainWindow.Activate();

        if (AppDistribution.GetLaunchFilePath() is { } wsbPath)
        {
            _ = mainWindow.ImportOnLaunchAsync(wsbPath);
        }
    }
}
