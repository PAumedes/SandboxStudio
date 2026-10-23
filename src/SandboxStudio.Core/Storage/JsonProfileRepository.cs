using System.Globalization;
using System.Text.Json;
using SandboxStudio.Core.Models;

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

    /// <summary>
    /// Current preset seed version; bump when presets are added so existing
    /// stores receive only the newly introduced ones.
    /// </summary>
    private const int CurrentSeedVersion = 2;

    /// <summary>
    /// The seed version in which each built-in preset was introduced, keyed by profile id.
    /// </summary>
    private static readonly IReadOnlyDictionary<Guid, int> PresetIntroducedInVersions =
        new Dictionary<Guid, int>
        {
            [Guid.Parse("11111111-1111-1111-1111-111111111111")] = 1,
            [Guid.Parse("22222222-2222-2222-2222-222222222222")] = 1,
            [Guid.Parse("33333333-3333-3333-3333-333333333333")] = 1,
            [Guid.Parse("44444444-4444-4444-4444-444444444444")] = 2,
            [Guid.Parse("55555555-5555-5555-5555-555555555555")] = 2,
        };

    private readonly string _profilesDirectory;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _disposed;
    private bool _directoryEnsured;

    /// <summary>
    /// Initializes a new instance with a specified or default storage directory.
    /// The storage directory is created lazily on first use.
    /// </summary>
    /// <param name="storageDirectory">Optional custom directory. Defaults to %LocalAppData%/SandboxStudio/Profiles.</param>
    public JsonProfileRepository(string? storageDirectory = null)
    {
        _profilesDirectory = storageDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SandboxStudio",
            "Profiles");
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

            // The marker content is the seed version reached so far; stores seeded
            // before versioning wrote an empty marker and count as version 1.
            var markerExists = File.Exists(initializedFilePath);
            var seededVersion = ReadSeedVersion(initializedFilePath);
            if (seededVersion >= CurrentSeedVersion)
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

            if (!File.Exists(initializingFilePath))
            {
                // A populated store whose marker is absent — or predates per-preset
                // tracking and holds no preset files — was curated manually; record
                // the current version but leave its contents untouched.
                var isCuratedStore = seededVersion <= InitialSeedVersion &&
                    Directory.GetFiles(_profilesDirectory, "*.json").Length > 0 &&
                    (!markerExists ||
                     !GetDefaultPresets().Any(preset => File.Exists(GetProfileFilePath(preset.Id))));
                if (isCuratedStore)
                {
                    await File.WriteAllTextAsync(
                        initializedFilePath,
                        CurrentSeedVersion.ToString(CultureInfo.InvariantCulture),
                        cancellationToken);
                    return;
                }

                await File.WriteAllTextAsync(initializingFilePath, string.Empty, cancellationToken);
            }

            foreach (var preset in GetDefaultPresets())
            {
                if (PresetIntroducedInVersions.GetValueOrDefault(preset.Id) <= seededVersion)
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
                CurrentSeedVersion.ToString(CultureInfo.InvariantCulture),
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
    public IReadOnlyList<SandboxProfile> GetDefaultPresets()
    {
        return new List<SandboxProfile>
        {
            new()
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Air-Gapped Malware Lab",
                Description = "Completely isolated environment with network disabled and clipboard redirection blocked.",
                Tags = new() { "Security", "Malware", "Isolated" },
                IconGlyph = "\uE72E", // Lock icon
                Configuration = new WsbConfiguration
                {
                    Networking = "Disable",
                    VGpu = "Disable",
                    ClipboardRedirection = "Disable",
                    AudioInput = "Disable",
                    VideoInput = "Disable",
                    ProtectedClient = "Enable"
                }
            },
            new()
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Name = "Ephemeral Web Browser",
                Description = "Clean, zero-residual web browsing sandbox with hardware GPU acceleration.",
                Tags = new() { "Privacy", "Web", "Safe" },
                IconGlyph = "\uE774", // Globe icon
                Configuration = new WsbConfiguration
                {
                    Networking = "Enable",
                    VGpu = "Enable",
                    MemoryInMB = 4096,
                    LogonCommand = new WsbLogonCommand
                    {
                        Command = "cmd.exe /c start msedge.exe --inprivate"
                    }
                }
            },
            new()
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Name = "Developer Workstation",
                Description = "Auto-installs Git and VS Code on boot via Winget with 8GB RAM allocated.",
                Tags = new() { "Development", "Coding", "Tools" },
                IconGlyph = "\uE7BE", // Code icon
                Configuration = new WsbConfiguration
                {
                    Networking = "Enable",
                    VGpu = "Enable",
                    MemoryInMB = 8192,
                    LogonCommand = new WsbLogonCommand
                    {
                        Command = "powershell.exe -ExecutionPolicy Bypass -Command \"winget install -e --id Microsoft.VisualStudioCode --accept-package-agreements --accept-source-agreements; winget install -e --id Git.Git --accept-package-agreements --accept-source-agreements\""
                    }
                }
            },
            new()
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                Name = "Conferencing Test Bed",
                Description = "Camera and microphone passthrough with networking for safely testing conferencing applications.",
                Tags = new() { "Testing", "Conferencing" },
                IconGlyph = "\uE714", // Webcam icon
                Configuration = new WsbConfiguration
                {
                    Networking = "Enable",
                    VGpu = "Enable",
                    MemoryInMB = 4096,
                    AudioInput = "Enable",
                    VideoInput = "Enable",
                    ClipboardRedirection = "Enable"
                }
            },
            new()
            {
                Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
                Name = "Isolated Printer Bench",
                Description = "Offline sandbox with host printer redirection for testing print-dependent applications.",
                Tags = new() { "Testing", "Printing", "Offline" },
                IconGlyph = "\uE749", // Print icon
                Configuration = new WsbConfiguration
                {
                    Networking = "Disable",
                    VGpu = "Disable",
                    MemoryInMB = 4096,
                    ClipboardRedirection = "Disable",
                    PrinterRedirection = "Enable"
                }
            }
        };
    }

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
