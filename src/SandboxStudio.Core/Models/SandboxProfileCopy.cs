namespace SandboxStudio.Core.Models;

/// <summary>
/// Creates independent editable copies of sandbox profiles.
/// </summary>
public static class SandboxProfileCopy
{
    /// <summary>
    /// Copies a profile and all mutable configuration objects.
    /// </summary>
    public static SandboxProfile Clone(SandboxProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var configuration = profile.Configuration;
        return new SandboxProfile
        {
            Id = profile.Id,
            Name = profile.Name,
            Description = profile.Description,
            Tags = profile.Tags.ToList(),
            IconGlyph = profile.IconGlyph,
            IsFavorite = profile.IsFavorite,
            CreatedAt = profile.CreatedAt,
            LastLaunchedAt = profile.LastLaunchedAt,
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
