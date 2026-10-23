using System.Text;
using System.Text.RegularExpressions;
using SandboxStudio.Core.Models;

namespace SandboxStudio.Core.Scripts;

/// <inheritdoc cref="ILogonScriptService"/>
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

    /// <summary>
    /// Initializes a new instance of <see cref="LogonScriptService"/>.
    /// </summary>
    /// <param name="scriptsDirectory">Optional custom script directory. Defaults to %LocalAppData%/SandboxStudio/Scripts.</param>
    public LogonScriptService(string? scriptsDirectory = null)
    {
        _scriptsDirectory = scriptsDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SandboxStudio",
            "Scripts");
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
    public async Task ApplyAsync(
        WsbConfiguration configuration,
        Guid profileId,
        IReadOnlyList<string> commands,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(commands);
        if (commands.Count == 0)
        {
            throw new ArgumentException("At least one command is required to build a logon script.", nameof(commands));
        }

        Directory.CreateDirectory(_scriptsDirectory);
        var script = new StringBuilder("@echo off");
        foreach (var command in commands)
        {
            script.Append("\r\n").Append(command);
        }

        await File.WriteAllTextAsync(GetScriptPath(profileId), script.Append("\r\n").ToString(), Utf8NoBom, cancellationToken);

        EnsureManagedMapping(configuration);
        configuration.LogonCommand = new WsbLogonCommand { Command = GetSandboxCommand(profileId) };
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
        DeleteScriptFile(profileId);
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

        var sourcePath = Path.Combine(_scriptsDirectory, $"{match.Groups[1].Value}.cmd");
        if (!File.Exists(sourcePath))
        {
            // The referenced script does not exist on this host; leave the command
            // as imported rather than fabricating a script.
            return Task.CompletedTask;
        }

        Directory.CreateDirectory(_scriptsDirectory);
        File.Copy(sourcePath, GetScriptPath(newProfileId), overwrite: true);
        EnsureManagedMapping(configuration);
        configuration.LogonCommand = new WsbLogonCommand { Command = GetSandboxCommand(newProfileId) };
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void PruneOrphans(IReadOnlySet<Guid> liveProfileIds)
    {
        ArgumentNullException.ThrowIfNull(liveProfileIds);
        if (!Directory.Exists(_scriptsDirectory))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(_scriptsDirectory, "*.cmd"))
        {
            var stem = Path.GetFileNameWithoutExtension(file);
            if (Guid.TryParseExact(stem, "N", out var id) && liveProfileIds.Contains(id))
            {
                continue;
            }

            try
            {
                File.Delete(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Best-effort sweep; a locked or read-only file stays behind.
            }
        }
    }

    private void EnsureManagedMapping(WsbConfiguration configuration)
    {
        configuration.MappedFolders ??= new List<WsbMappedFolder>();
        var managed = configuration.MappedFolders.FirstOrDefault(IsManagedMapping);
        if (managed is null)
        {
            configuration.MappedFolders.Add(new WsbMappedFolder
            {
                HostFolder = _scriptsDirectory,
                SandboxFolder = SandboxScriptDirectory,
                IsReadOnly = true
            });
            return;
        }

        // Repair drift from the editable folder row (or an import that rewrote paths).
        managed.HostFolder = _scriptsDirectory;
        managed.IsReadOnly = true;
    }

    private string GetScriptPath(Guid profileId) =>
        Path.Combine(_scriptsDirectory, $"{profileId:N}.cmd");

    private static string GetSandboxCommand(Guid profileId) =>
        $"cmd.exe /c \"{SandboxScriptDirectory}\\{profileId:N}.cmd\"";

    private void DeleteScriptFile(Guid profileId)
    {
        var scriptPath = GetScriptPath(profileId);
        if (File.Exists(scriptPath))
        {
            File.Delete(scriptPath);
        }
    }
}
