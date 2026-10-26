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

    private readonly string _profilesDirectory;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _disposed;
    private bool _legacyMarkersRemoved;

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
        // CreateDirectory is idempotent; skipping it on a remembered flag broke
        // repositories whose directory was deleted mid-session.
        Directory.CreateDirectory(_profilesDirectory);

        if (!_legacyMarkersRemoved)
        {
            // .initialized/.initializing are inert litter left by the removed
            // preset-seeding machinery; delete them once per repository instance.
            foreach (var marker in new[] { ".initialized", ".initializing" })
            {
                try
                {
                    File.Delete(Path.Combine(_profilesDirectory, marker));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Cosmetic cleanup only.
                }
            }

            _legacyMarkersRemoved = true;
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
