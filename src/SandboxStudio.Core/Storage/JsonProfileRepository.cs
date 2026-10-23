using System.Globalization;
using System.Text.Json;
using SandboxStudio.Core.Capabilities;
using SandboxStudio.Core.Models;
using SandboxStudio.Core.Presets;

namespace SandboxStudio.Core.Storage;

/// <summary>
/// JSON file-based repository implementation storing sandbox profiles in the local user profile.
/// </summary>
public sealed class JsonProfileRepository : IProfileRepository, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Seed version shipped before preset versioning existed.
    /// </summary>
    private const int InitialSeedVersion = 1;

    private readonly string _profilesDirectory;
    private readonly CuratedPresets _presets;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _disposed;
    private bool _directoryEnsured;

    /// <summary>
    /// Initializes a new instance with a specified or default storage directory.
    /// The storage directory is created lazily on first use.
    /// </summary>
    /// <param name="storageDirectory">Optional custom directory. Defaults to %LocalAppData%/SandboxStudio/Profiles.</param>
    /// <param name="presets">Curated preset catalog; defaults to one built from live host capability probes.</param>
    public JsonProfileRepository(string? storageDirectory = null, CuratedPresets? presets = null)
    {
        _profilesDirectory = storageDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SandboxStudio",
            "Profiles");
        _presets = presets ?? new CuratedPresets(new HostCapabilities());
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SandboxProfile>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            EnsureStorageDirectory();
            var files = Directory.GetFiles(_profilesDirectory, "*.json");
            var profiles = new List<SandboxProfile>();

            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file, cancellationToken);
                    var profile = JsonSerializer.Deserialize<SandboxProfile>(json, JsonOptions);
                    if (profile is not null)
                    {
                        Normalize(profile);
                        profiles.Add(profile);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Ignore corrupted individual files to preserve application availability
                }
            }

            return profiles.OrderByDescending(p => p.IsFavorite)
                           .ThenBy(p => p.Name)
                           .ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task EnsureDefaultPresetsAsync(CancellationToken cancellationToken = default)
    {
        var initializedFilePath = Path.Combine(_profilesDirectory, ".initialized");
        var initializingFilePath = Path.Combine(_profilesDirectory, ".initializing");

        await _lock.WaitAsync(cancellationToken);
        try
        {
            EnsureStorageDirectory();

            // The marker content is the seed version processed so far, relative to
            // the host that seeded it — a store stamped on a host whose preset
            // catalog excluded a conditional preset is not revisited if that preset
            // later becomes applicable (the store lives in %LocalAppData%, so the
            // host catalog rarely changes beneath it). Stores seeded before
            // versioning wrote an empty marker and count as version 1.
            var markerExists = File.Exists(initializedFilePath);
            var seededVersion = ReadSeedVersion(initializedFilePath);
            if (seededVersion >= CuratedPresets.CurrentSeedVersion)
            {
                // Remove an .initializing marker orphaned by an interrupted concurrent seed.
                try
                {
                    File.Delete(initializingFilePath);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Cosmetic cleanup only.
                }

                return;
            }

            var presets = _presets.GetDefaultPresets();

            if (!File.Exists(initializingFilePath))
            {
                // A populated store whose marker is absent — or was stamped without
                // any preset files ever being written — was curated manually; record
                // the current version but leave its contents untouched. Preset-file
                // presence is judged against every known preset id, not the
                // host-filtered list, so conditional presets count on any host.
                var isCuratedStore = seededVersion < CuratedPresets.CurrentSeedVersion &&
                    Directory.GetFiles(_profilesDirectory, "*.json").Length > 0 &&
                    (!markerExists ||
                     !CuratedPresets.IntroducedInVersions.Keys.Any(id => File.Exists(GetProfileFilePath(id))));
                if (isCuratedStore)
                {
                    await File.WriteAllTextAsync(
                        initializedFilePath,
                        CuratedPresets.CurrentSeedVersion.ToString(CultureInfo.InvariantCulture),
                        cancellationToken);
                    return;
                }

                await File.WriteAllTextAsync(initializingFilePath, string.Empty, cancellationToken);
            }

            foreach (var preset in presets)
            {
                if (CuratedPresets.IntroducedInVersions.GetValueOrDefault(preset.Id) <= seededVersion)
                {
                    continue;
                }

                var filePath = GetProfileFilePath(preset.Id);
                if (!File.Exists(filePath))
                {
                    await WriteProfileAsync(preset, cancellationToken);
                }
            }

            await File.WriteAllTextAsync(
                initializingFilePath,
                CuratedPresets.CurrentSeedVersion.ToString(CultureInfo.InvariantCulture),
                cancellationToken);

            try
            {
                File.Move(initializingFilePath, initializedFilePath, overwrite: true);
            }
            catch (IOException) when (File.Exists(initializedFilePath))
            {
                // A concurrent process completed initialization first; treat as initialized.
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Reads the preset seed version recorded in the marker file: absent file is
    /// version 0, while empty or unparsable content marks a store seeded before
    /// versioning existed.
    /// </summary>
    private static int ReadSeedVersion(string initializedFilePath)
    {
        if (!File.Exists(initializedFilePath))
        {
            return 0;
        }

        var content = File.ReadAllText(initializedFilePath).Trim();
        return int.TryParse(content, NumberStyles.Integer, CultureInfo.InvariantCulture, out var version)
            ? version
            : InitialSeedVersion;
    }

    /// <inheritdoc/>
    public async Task<SandboxProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filePath = GetProfileFilePath(id);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            var profile = JsonSerializer.Deserialize<SandboxProfile>(json, JsonOptions);
            if (profile is not null)
            {
                Normalize(profile);
            }

            return profile;
        }
        catch (JsonException)
        {
            return null; // Return null if file is corrupt rather than crashing caller
        }
        catch (FileNotFoundException)
        {
            return null; // File was deleted between the existence check and the read
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task SaveAsync(SandboxProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            await WriteProfileAsync(profile, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filePath = GetProfileFilePath(id);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                return true;
            }

            return false;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<SandboxProfile> GetDefaultPresets() => _presets.GetDefaultPresets();

    private string GetProfileFilePath(Guid id) => Path.Combine(_profilesDirectory, $"{id:N}.json");

    /// <summary>
    /// Repairs members that deserializers can leave null on corrupt-but-loadable files
    /// so downstream consumers never see a null required member.
    /// </summary>
    private static void Normalize(SandboxProfile profile)
    {
        profile.Tags ??= new();
        profile.Configuration ??= new WsbConfiguration();
        profile.Configuration.MappedFolders ??= new();
    }

    private void EnsureStorageDirectory()
    {
        if (!_directoryEnsured)
        {
            Directory.CreateDirectory(_profilesDirectory);
            _directoryEnsured = true;
        }
    }

    private async Task WriteProfileAsync(SandboxProfile profile, CancellationToken cancellationToken)
    {
        var filePath = GetProfileFilePath(profile.Id);
        var tempFilePath = $"{filePath}.{Guid.NewGuid():N}.tmp";
        var json = JsonSerializer.Serialize(profile, JsonOptions);

        EnsureStorageDirectory();

        try
        {
            await File.WriteAllTextAsync(tempFilePath, json, cancellationToken);
            File.Move(tempFilePath, filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                try
                {
                    File.Delete(tempFilePath);
                }
                catch
                {
                    // Best effort cleanup
                }
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_disposed)
        {
            _lock.Dispose();
            _disposed = true;
        }
    }
}
