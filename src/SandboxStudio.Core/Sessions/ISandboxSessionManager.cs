namespace SandboxStudio.Core.Sessions;

/// <summary>
/// Discovers and controls live Windows Sandbox sessions through the <c>wsb</c> CLI
/// (Windows 11 24H2+).
/// </summary>
public interface ISandboxSessionManager
{
    /// <summary>Whether the <c>wsb</c> CLI is usable on this host.</summary>
    bool IsSupported { get; }

    /// <summary>Lists the sessions reported for the current user.</summary>
    Task<WsbListResult> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a new session from a formatted <c>&lt;Configuration&gt;</c> XML string
    /// (<c>wsb start --config</c>) and reports the sandbox ID the CLI assigned.
    /// </summary>
    Task<WsbStartResult> StartAsync(string configurationXml, CancellationToken cancellationToken = default);

    /// <summary>Stops a session, releasing its resources and closing its window.</summary>
    Task<WsbOperationResult> StopAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Opens a remote desktop window onto a session.</summary>
    Task<WsbOperationResult> ConnectAsync(Guid id, CancellationToken cancellationToken = default);
}
