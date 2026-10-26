using System.Reflection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SandboxStudio.Core.Storage;

namespace SandboxStudio.UI.Views;

/// <summary>
/// Application settings dialog: theme, display language, quick-start access,
/// and the about section.
/// </summary>
public sealed partial class SettingsDialog : ContentDialog
{
    private readonly IAppSettingsStore _settingsStore;
    private AppSettings _settings;
    private bool _initialized;

    /// <summary>
    /// Gets a value indicating whether the chosen language differs from the one
    /// the app is running in, so a restart is required to apply it.
    /// </summary>
    public bool LanguageRestartRequired { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the user asked to see the quick-start guide.
    /// </summary>
    public bool QuickStartRequested { get; private set; }

    /// <summary>
    /// Initializes a new instance of <see cref="SettingsDialog"/>.
    /// </summary>
    public SettingsDialog(IAppSettingsStore settingsStore)
    {
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _settings = settingsStore.Load();

        InitializeComponent();

        ThemeComboBox.SelectedIndex = _settings.Theme switch
        {
            "Light" => 1,
            "Dark" => 2,
            _ => 0
        };
        var languageIndex = LanguageComboBox.Items
            .OfType<ComboBoxItem>()
            .ToList()
            .FindIndex(item => string.Equals(item.Tag as string, _settings.LanguageTag, StringComparison.OrdinalIgnoreCase));
        LanguageComboBox.SelectedIndex = languageIndex < 0 ? 0 : languageIndex;

        VersionTextBlock.Text = Localizer.Format("AboutVersionFormat", VersionLabel);

        // Reflect restart state already pending from earlier sessions, not just
        // changes made in this dialog instance.
        LanguageRestartRequired = IsRestartPending(_settings.LanguageTag);
        LanguageRestartHint.Visibility =
            LanguageRestartRequired ? Visibility.Visible : Visibility.Collapsed;

        _initialized = true;
    }

    // The informational version carries a "+commit" suffix in CI builds; the
    // about page shows the product version only.
    private static string VersionLabel =>
        (Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "0.0.0").Split('+')[0];

    private void OnThemeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized || sender is not ComboBox { SelectedItem: ComboBoxItem item })
        {
            return;
        }

        // Live preview: RequestedTheme on the window root re-themes the whole app.
        var theme = item.Tag as string;
        if (XamlRoot?.Content is FrameworkElement root)
        {
            root.RequestedTheme = theme switch
            {
                "Light" => ElementTheme.Light,
                "Dark" => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
        }

        var newTheme = string.IsNullOrEmpty(theme) ? null : theme;
        if (SaveSettings(_settings with { Theme = newTheme }))
        {
            _settings = _settings with { Theme = newTheme };
        }
    }

    private void OnLanguageSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized || sender is not ComboBox { SelectedItem: ComboBoxItem item })
        {
            return;
        }

        var tag = item.Tag as string;
        var newTag = string.IsNullOrEmpty(tag) ? null : tag;
        if (string.Equals(_settings.LanguageTag, newTag, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!SaveSettings(_settings with { LanguageTag = newTag }))
        {
            return;
        }

        _settings = _settings with { LanguageTag = newTag };

        LanguageRestartRequired = IsRestartPending(newTag);
        LanguageRestartHint.Visibility =
            LanguageRestartRequired ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Compares the language the app would launch in under
    /// <paramref name="storedTag"/> with the one it is running in. Null/empty or
    /// unsupported tags resolve through the OS preference list.
    /// </summary>
    private static bool IsRestartPending(string? storedTag)
    {
        var target = Localizer.IsSupportedLanguage(storedTag)
            ? storedTag
            : Localizer.ResolveSystemTag();
        return !string.Equals(target, Localizer.RunningLanguageTag(), StringComparison.OrdinalIgnoreCase);
    }

    private void OnShowQuickStartClicked(object sender, RoutedEventArgs e)
    {
        QuickStartRequested = true;
        Hide();
    }

    /// <summary>
    /// A closed ComboBox consumes the mouse wheel to cycle its selection; with no
    /// ScrollViewer to scroll, the wheel is swallowed to keep scrolling gestures
    /// from silently changing settings.
    /// </summary>
    private void OnComboBoxWheel(object sender, PointerRoutedEventArgs e)
    {
        if (sender is ComboBox { IsDropDownOpen: false })
        {
            e.Handled = true;
        }
    }

    private bool SaveSettings(AppSettings settings)
    {
        try
        {
            _settingsStore.Save(settings);
            return true;
        }
        catch (Exception ex)
        {
            ErrorTextBlock.Text = Localizer.Format("StatusSettingsSaveError", ex.Message);
            ErrorTextBlock.Visibility = Visibility.Visible;
            return false;
        }
    }
}
