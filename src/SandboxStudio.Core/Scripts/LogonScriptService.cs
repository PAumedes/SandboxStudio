using System.Text;
using System.Text.RegularExpressions;
using SandboxStudio.Core.Models;

namespace SandboxStudio.Core.Scripts;

/// <inheritdoc cref="ILogonScriptService"/>
/// <remarks>
/// Each profile's script lives in its own host directory
/// (<c>Scripts\&lt;id&gt;\&lt;id&gt;.cmd</c>) and only that directory is mounted, so a
/// sandbox never sees another profile's setup. The sandbox-side path stays
/// <c>C:\SandboxStudio\Scripts\&lt;id&gt;.cmd</c> for every profile.
/// The scripts live under <c>%USERPROFILE%\.sandboxstudio</c> because Windows
/// Sandbox fails to start (0x80070002) when a mapped host folder sits under
/// <c>%APPDATA%</c> or <c>%LOCALAPPDATA%</c>, where earlier releases kept them.
/// </remarks>
public sealed partial class LogonScriptService : ILogonScriptService
{
    /// <summary>
    /// Absolute path inside the sandbox where the managed script directory is mounted.
    /// </summary>
    public const string SandboxScriptDirectory = @"C:\SandboxStudio\Scripts";

    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    [GeneratedRegex(@"Scripts\\([0-9A-Fa-f]{32})\.cmd", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ManagedCommandPattern();

    private readonly string _scriptsDirectory;
    private readonly string? _legacyScriptsDirectory;

    /// <summary>
    /// Initializes a new instance of <see cref="LogonScriptService"/>.
    /// </summary>
    /// <param name="scriptsDirectory">Optional custom script directory. Defaults to %USERPROFILE%/.sandboxstudio/Scripts.</param>
    /// <param name="legacyScriptsDirectory">Optional directory that earlier releases wrote scripts to; its scripts
    /// are migrated into the script directory. Defaults to %LocalAppData%/SandboxStudio/Scripts when
    /// <paramref name="scriptsDirectory"/> is not supplied.</param>
    public LogonScriptService(string? scriptsDirectory = null, string? legacyScriptsDirectory = null)
    {
        _scriptsDirectory = scriptsDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".sandboxstudio",
            "Scripts");
        _legacyScriptsDirectory = legacyScriptsDirectory ?? (scriptsDirectory is null
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SandboxStudio",
                "Scripts")
            : null);
    }

    /// <inheritdoc/>
    public bool IsScriptBacked(WsbConfiguration configuration, Guid profileId)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return string.Equals(
            configuration.LogonCommand?.Command,
            GetSandboxCommand(profileId),
            StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public bool IsManagedMapping(WsbMappedFolder folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        // The sandbox-side mount point is the capability's identity — user content
        // has no legitimate reason to map a folder there.
        return string.Equals(folder.SandboxFolder, SandboxScriptDirectory, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public bool HasNonAsciiContent(IEnumerable<string> commands) =>
        commands.Any(command => command.Any(c => c > 0x7F));

    /// <inheritdoc/>
    public void Configure(WsbConfiguration configuration, Guid profileId)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        // The mount target must exist before validation runs, or the validator
        // reports the not-yet-written script directory as a missing host folder.
        Directory.CreateDirectory(GetProfileScriptDirectory(profileId));
        EnsureManagedMapping(configuration, profileId);
        configuration.LogonCommand = new WsbLogonCommand { Command = GetSandboxCommand(profileId) };
    }

    /// <inheritdoc/>
    public async Task WriteScriptAsync(
        Guid profileId,
        IReadOnlyList<string> commands,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(commands);
        if (commands.Count == 0)
        {
            throw new ArgumentException("At least one command is required to build a logon script.", nameof(commands));
        }

        Directory.CreateDirectory(GetProfileScriptDirectory(profileId));
        var script = new StringBuilder("@echo off");
        foreach (var command in commands)
        {
            script.Append("\r\n").Append(command);
        }

        await File.WriteAllTextAsync(GetScriptPath(profileId), script.Append("\r\n").ToString(), Utf8NoBom, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task ApplyAsync(
        WsbConfiguration configuration,
        Guid profileId,
        IReadOnlyList<string> commands,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        await WriteScriptAsync(profileId, commands, cancellationToken);
        Configure(configuration, profileId);
    }

    /// <inheritdoc/>
    public Task UnapplyAsync(
        WsbConfiguration configuration,
        Guid profileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (IsScriptBacked(configuration, profileId))
        {
            configuration.LogonCommand = null;
        }

        var commandStillNeedsMount = configuration.LogonCommand?.Command
            ?.Contains(SandboxScriptDirectory, StringComparison.OrdinalIgnoreCase) == true;
        if (!commandStillNeedsMount && configuration.MappedFolders is { } folders)
        {
            folders.RemoveAll(IsManagedMapping);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task DeleteAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        DeleteScriptFiles(profileId);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task RehomeAsync(
        WsbConfiguration configuration,
        Guid newProfileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var command = configuration.LogonCommand?.Command;
        var match = ManagedCommandPattern().Match(command ?? string.Empty);
        if (!match.Success || string.Equals(match.Groups[1].Value, $"{newProfileId:N}", StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        var sourcePath = GetScriptCandidates(match.Groups[1].Value).FirstOrDefault(File.Exists);
        if (sourcePath is null)
        {
            // The referenced script does not exist on this host; leave the command
            // as imported rather than fabricating a script.
            return Task.CompletedTask;
        }

        Directory.CreateDirectory(GetProfileScriptDirectory(newProfileId));
        File.Copy(sourcePath, GetScriptPath(newProfileId), overwrite: true);
        Configure(configuration, newProfileId);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void PruneOrphans(IReadOnlySet<Guid> liveProfileIds)
    {
        ArgumentNullException.ThrowIfNull(liveProfileIds);
        foreach (var root in GetScriptRoots())
        {
            PruneRoot(root, liveProfileIds);
        }
    }

    private static void PruneRoot(string root, IReadOnlySet<Guid> liveProfileIds)
    {
        if (!Directory.Exists(root))
        {
            return;
        }

        foreach (var directory in Directory.EnumerateDirectories(root))
        {
            if (Guid.TryParseExact(Path.GetFileName(directory), "N", out var id) && !liveProfileIds.Contains(id))
            {
                TryDelete(() => Directory.Delete(directory, recursive: true));
            }
        }

        // Legacy flat-layout scripts (pre per-profile directories).
        foreach (var file in Directory.EnumerateFiles(root, "*.cmd"))
        {
            var stem = Path.GetFileNameWithoutExtension(file);
            if (Guid.TryParseExact(stem, "N", out var id) && liveProfileIds.Contains(id))
            {
                continue;
            }

            TryDelete(() => File.Delete(file));
        }

        static void TryDelete(Action delete)
        {
            try
            {
                delete();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Best-effort sweep; a locked or read-only entry stays behind.
            }
        }
    }

    /// <inheritdoc/>
    public bool MigrateLegacyLayout(WsbConfiguration configuration, Guid profileId)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (!IsScriptBacked(configuration, profileId))
        {
            return false;
        }

        var profileDirectory = GetProfileScriptDirectory(profileId);
        var managed = configuration.MappedFolders?.FirstOrDefault(IsManagedMapping);
        if (managed is not null &&
            managed.IsReadOnly &&
            string.Equals(Path.TrimEndingDirectorySeparator(managed.HostFolder), profileDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var scriptPath = GetScriptPath(profileId);
        var legacyPath = GetScriptCandidates($"{profileId:N}").Skip(1).FirstOrDefault(File.Exists);
        if (legacyPath is not null && !File.Exists(scriptPath))
        {
            Directory.CreateDirectory(profileDirectory);
            File.Move(legacyPath, scriptPath);
            TryDeleteEmptyDirectory(Path.GetDirectoryName(legacyPath)!);
        }

        // Rewire the mount even when no script is found, so the sandbox neither
        // reads the shared directory nor fails on an AppData host folder.
        EnsureManagedMapping(configuration, profileId);
        return true;
    }

    private void EnsureManagedMapping(WsbConfiguration configuration, Guid profileId)
    {
        configuration.MappedFolders ??= new List<WsbMappedFolder>();
        var hostFolder = GetProfileScriptDirectory(profileId);
        var managed = configuration.MappedFolders.FirstOrDefault(IsManagedMapping);
        if (managed is null)
        {
            configuration.MappedFolders.Add(new WsbMappedFolder
            {
                HostFolder = hostFolder,
                SandboxFolder = SandboxScriptDirectory,
                IsReadOnly = true
            });
            return;
        }

        // Repair drift from the editable folder row (or an import that rewrote paths).
        managed.HostFolder = hostFolder;
        managed.IsReadOnly = true;
    }

    private string GetProfileScriptDirectory(Guid profileId) =>
        Path.Combine(_scriptsDirectory, $"{profileId:N}");

    private string GetScriptPath(Guid profileId) =>
        Path.Combine(GetProfileScriptDirectory(profileId), $"{profileId:N}.cmd");

    private string[] GetScriptRoots() =>
        _legacyScriptsDirectory is null ? [_scriptsDirectory] : [_scriptsDirectory, _legacyScriptsDirectory];

    /// <summary>
    /// Where a profile's script may live, current location first: the
    /// per-profile directory and the older flat layout, under each script root.
    /// </summary>
    private IEnumerable<string> GetScriptCandidates(string profileId) =>
        GetScriptRoots().SelectMany(root => new[]
        {
            Path.Combine(root, profileId, $"{profileId}.cmd"),
            Path.Combine(root, $"{profileId}.cmd")
        });

    private void TryDeleteEmptyDirectory(string directory)
    {
        if (GetScriptRoots().Contains(directory, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            if (!Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort; an empty directory left behind is harmless.
        }
    }

    private static string GetSandboxCommand(Guid profileId) =>
        $"cmd.exe /c \"{SandboxScriptDirectory}\\{profileId:N}.cmd\"";

    private void DeleteScriptFiles(Guid profileId)
    {
        foreach (var root in GetScriptRoots())
        {
            var profileDirectory = Path.Combine(root, $"{profileId:N}");
            if (Directory.Exists(profileDirectory))
            {
                Directory.Delete(profileDirectory, recursive: true);
            }

            var flatPath = Path.Combine(root, $"{profileId:N}.cmd");
            if (File.Exists(flatPath))
            {
                File.Delete(flatPath);
            }
        }
    }
}
