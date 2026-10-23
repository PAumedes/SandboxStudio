namespace SandboxStudio.Core.Sessions;

/// <summary>
/// Result of <c>wsb start</c>: the sandbox ID assigned by the CLI on success.
/// </summary>
/// <param name="IsSuccess">Whether a session was started.</param>
/// <param name="SessionId">The sandbox ID the CLI reported; <see langword="null"/> on failure.</param>
/// <param name="ErrorMessage">Why the start failed, when it did.</param>
/// <param name="CliNeverStarted">
/// Whether the CLI provably never ran, making a fallback launch path safe.
/// <see langword="false"/> means a session may already exist — callers must not retry.
/// </param>
public sealed record WsbStartResult(bool IsSuccess, Guid? SessionId, string? ErrorMessage, bool CliNeverStarted = false)
{
    /// <summary>Factory for a successful result.</summary>
    public static WsbStartResult Success(Guid sessionId) => new(true, sessionId, null);

    /// <summary>Factory for a failed result.</summary>
    public static WsbStartResult Failure(string errorMessage, bool cliNeverStarted = false) =>
        new(false, null, errorMessage, cliNeverStarted);
}
