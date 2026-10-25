using SandboxStudio.Core.Models;

namespace SandboxStudio.Core.Scripts;

/// <summary>
/// Packages a profile's multi-step logon setup into a <c>.cmd</c> script delivered
/// to the sandbox through a managed read-only mapped folder, per Microsoft's
/// recommended pattern for complex logon commands.
/// </summary>
public interface ILogonScriptService
{
    /// <summary>
    /// Returns whether the configuration's logon command points at the managed
    /// script for <paramref name="profileId"/>.
    /// </summary>
    bool IsScriptBacked(WsbConfiguration configuration, Guid profileId);

    /// <summary>
    /// Returns whether <paramref name="folder"/> is the managed scripts mount
    /// (identified by its sandbox-side path).
    /// </summary>
    bool IsManagedMapping(WsbMappedFolder folder);

    /// <summary>
    /// Returns whether any command contains non-ASCII characters, which cmd.exe
    /// may not decode correctly in a batch file.
    /// </summary>
    bool HasNonAsciiContent(IEnumerable<string> commands);

    /// <summary>
    /// Rewires the configuration without touching disk: a read-only mapped folder
    /// for the profile's own script directory (added or repaired) plus a
    /// <see cref="WsbConfiguration.LogonCommand"/> that invokes the script.
    /// </summary>
    void Configure(WsbConfiguration configuration, Guid profileId);

    /// <summary>
    /// Writes the script file for <paramref name="profileId"/>. Callers that may
    /// still abandon the edit should validate first, since the stored profile
    /// runs whatever this file contains.
    /// </summary>
    Task WriteScriptAsync(
        Guid profileId,
        IReadOnlyList<string> commands,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the script file and rewires the configuration
    /// (<see cref="WriteScriptAsync"/> followed by <see cref="Configure"/>).
    /// </summary>
    Task ApplyAsync(
        WsbConfiguration configuration,
        Guid profileId,
        IReadOnlyList<string> commands,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the managed mount and clears the logon command when it references
    /// the managed script for <paramref name="profileId"/>. The mount survives
    /// while another command (for example, an imported profile's script path)
    /// still needs it. The script file is left in place — call
    /// <see cref="DeleteAsync"/> once the teardown is known to stick, so a
    /// cancelled save cannot leave a profile pointing at a deleted file.
    /// </summary>
    Task UnapplyAsync(
        WsbConfiguration configuration,
        Guid profileId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the script file for a profile (after removal or an unapplied save).
    /// </summary>
    Task DeleteAsync(Guid profileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retargets a managed-script command that references another profile's id
    /// (for example, an imported .wsb) to <paramref name="newProfileId"/>,
    /// copying the script file when it exists on this host.
    /// </summary>
    Task RehomeAsync(
        WsbConfiguration configuration,
        Guid newProfileId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes script files and directories whose profile id is not in <paramref name="liveProfileIds"/>.
    /// </summary>
    void PruneOrphans(IReadOnlySet<Guid> liveProfileIds);

    /// <summary>
    /// Moves a script-backed profile off the legacy shared layout, where the
    /// managed mount exposed every profile's script to every sandbox, onto its
    /// own script directory.
    /// </summary>
    /// <returns><see langword="true"/> when the configuration changed and must be saved.</returns>
    bool MigrateLegacyLayout(WsbConfiguration configuration, Guid profileId);
}
