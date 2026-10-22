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

    /// <summary>
    /// Initializes a new instance with a specified or default storage directory.
    /// </summary>
    /// <param name="storageDirectory">Optional custom directory. Defaults to %LocalAppData%/SandboxStudio/Profiles.</param>
    public JsonProfileRepository(string? storageDirectory = null)
    {
        _profilesDirectory = storageDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SandboxStudio",
            "Profiles");

        if (!Directory.Exists(_profilesDirectory))
        {
            Directory.CreateDirectory(_profilesDirectory);
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SandboxProfile>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
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
                        profiles.Add(profile);
                    }
                }
                catch
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
            return JsonSerializer.Deserialize<SandboxProfile>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null; // Return null if file is corrupt rather than crashing caller
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

        var filePath = GetProfileFilePath(profile.Id);
        var tempFilePath = $"{filePath}.tmp";

        var json = JsonSerializer.Serialize(profile, JsonOptions);

        await _lock.WaitAsync(cancellationToken);
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
            }
        };
    }

    private string GetProfileFilePath(Guid id) => Path.Combine(_profilesDirectory, $"{id:N}.json");

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
