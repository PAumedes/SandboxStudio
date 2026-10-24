namespace SandboxStudio.Core.Updates;

/// <summary>
/// Checks a release feed for a newer version. Discovery only — the checker never
/// downloads, installs, or overwrites anything.
/// </summary>
public interface IUpdateChecker
{
    /// <summary>Compares the running version against the latest published release.</summary>
    Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default);
}
