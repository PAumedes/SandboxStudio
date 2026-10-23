using System.Text.Json;

namespace SandboxStudio.Core.Storage;

/// <summary>
/// JSON-backed <see cref="IAppSettingsStore"/> rooted at
/// <c>%LocalAppData%\SandboxStudio\settings.json</c>.
/// </summary>
public sealed class JsonAppSettingsStore : IAppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _settingsFilePath;

    /// <summary>
    /// Initializes a new instance of <see cref="JsonAppSettingsStore"/>.
    /// </summary>
    /// <param name="settingsFilePath">Optional custom settings file path. Defaults to %LocalAppData%/SandboxStudio/settings.json.</param>
    public JsonAppSettingsStore(string? settingsFilePath = null)
    {
        _settingsFilePath = settingsFilePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SandboxStudio",
            "settings.json");
    }

    /// <inheritdoc/>
    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                return new AppSettings();
            }

            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_settingsFilePath))
                ?? new AppSettings();
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
        catch (IOException)
        {
            return new AppSettings();
        }
        catch (UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    /// <inheritdoc/>
    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var directory = Path.GetDirectoryName(_settingsFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_settingsFilePath, JsonSerializer.Serialize(settings, JsonOptions));
    }
}
