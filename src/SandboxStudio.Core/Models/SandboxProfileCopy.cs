namespace SandboxStudio.Core.Models;

/// <summary>
/// Creates independent editable copies of sandbox profiles.
/// </summary>
public static class SandboxProfileCopy
{
    /// <summary>
    /// Copies a profile and all mutable configuration objects, preserving its identity
    /// and bookkeeping fields.
    /// </summary>
    public static SandboxProfile Clone(SandboxProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return Copy(profile, profile.Id, profile.CreatedAt, profile.IsFavorite, profile.LastLaunchedAt);
    }

    /// <summary>
    /// Copies a profile as a new library entry: fresh identifier and creation timestamp,
    /// no launch history, and not marked as favorite. The name is unchanged; callers
    /// assign a distinct name before persisting.
    /// </summary>
    public static SandboxProfile Duplicate(SandboxProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return Copy(
            profile,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            isFavorite: false,
            lastLaunchedAt: null);
    }

    private static SandboxProfile Copy(
        SandboxProfile profile,
        Guid id,
        DateTimeOffset createdAt,
        bool isFavorite,
        DateTimeOffset? lastLaunchedAt)
    {
        var configuration = profile.Configuration;
        return new SandboxProfile
        {
            Id = id,
            Name = profile.Name,
            Description = profile.Description,
            Tags = profile.Tags.ToList(),
            IconGlyph = profile.IconGlyph,
            LogonScriptCommands = profile.LogonScriptCommands?.ToList(),
            IsFavorite = isFavorite,
            CreatedAt = createdAt,
            LastLaunchedAt = lastLaunchedAt,
            Configuration = new WsbConfiguration
            {
                VGpu = configuration.VGpu,
                Networking = configuration.Networking,
                MemoryInMB = configuration.MemoryInMB,
                AudioInput = configuration.AudioInput,
                VideoInput = configuration.VideoInput,
                ProtectedClient = configuration.ProtectedClient,
                ClipboardRedirection = configuration.ClipboardRedirection,
                PrinterRedirection = configuration.PrinterRedirection,
                MappedFolders = configuration.MappedFolders
                    .Select(CloneMappedFolder)
                    .ToList(),
                LogonCommand = configuration.LogonCommand is null
                    ? null
                    : new WsbLogonCommand { Command = configuration.LogonCommand.Command }
            }
        };
    }

    private static WsbMappedFolder CloneMappedFolder(WsbMappedFolder folder) => new()
    {
        HostFolder = folder.HostFolder,
        SandboxFolder = folder.SandboxFolder,
        ReadOnly = folder.ReadOnly
    };
}
