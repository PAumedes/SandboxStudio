namespace SandboxStudio.Core.Storage;

/// <summary>
/// Persists <see cref="AppSettings"/> outside the profile store.
/// </summary>
public interface IAppSettingsStore
{
    /// <summary>Loads the current settings, returning defaults when none are stored.</summary>
    AppSettings Load();

    /// <summary>Persists the settings, replacing any previously stored values.</summary>
    void Save(AppSettings settings);
}
