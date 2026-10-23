namespace SandboxStudio.Core.Sessions;

/// <summary>
/// A live Windows Sandbox session reported by the <c>wsb</c> CLI.
/// </summary>
public sealed record SandboxSession
{
    /// <summary>The identifier other CLI commands take to address this session.</summary>
    public required Guid Id { get; init; }

    /// <summary>Reported session state; <see cref="SandboxSessionState.Unknown"/> when the CLI does not report one.</summary>
    public required SandboxSessionState State { get; init; }

    /// <summary>How long the session has been running, when the CLI reports it.</summary>
    public TimeSpan? Uptime { get; init; }
}
