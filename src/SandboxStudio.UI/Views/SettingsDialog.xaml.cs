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

        ThemeComboBox.SelectedIndex = ThemeIndex(_settings.Theme);
        LanguageComboBox.SelectedIndex = LanguageIndex(_settings.LanguageTag);

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

        var theme = item.Tag as string;
        var newTheme = string.IsNullOrEmpty(theme) ? null : theme;
        if (!SaveSettings(_settings with { Theme = newTheme }))
        {
            // Keep the picker on the theme that is actually stored.
            RevertSelection(ThemeComboBox, ThemeIndex(_settings.Theme));
            return;
        }

        _settings = _settings with { Theme = newTheme };

        // Live preview: RequestedTheme on the window root re-themes the app; this
        // dialog sits in the popup layer and does not inherit it, so set it too.
        var elementTheme = AppTheme.FromSetting(newTheme);
        if (XamlRoot?.Content is FrameworkElement root)
        {
            root.RequestedTheme = elementTheme;
        }

        RequestedTheme = elementTheme;
    }

    private static int ThemeIndex(string? theme) => theme switch
    {
        "Light" => 1,
        "Dark" => 2,
        _ => 0
    };

    private int LanguageIndex(string? languageTag)
    {
        var index = LanguageComboBox.Items
            .OfType<ComboBoxItem>()
            .ToList()
            .FindIndex(item => string.Equals(item.Tag as string, languageTag, StringComparison.OrdinalIgnoreCase));
        return index < 0 ? 0 : index;
    }

    private void RevertSelection(ComboBox comboBox, int index)
    {
        _initialized = false;
        comboBox.SelectedIndex = index;
        _initialized = true;
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
            RevertSelection(LanguageComboBox, LanguageIndex(_settings.LanguageTag));
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

    private void OnComboBoxWheel(object sender, PointerRoutedEventArgs e) =>
        ComboBoxWheel.Redirect(sender, e);

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
