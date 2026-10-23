namespace SandboxStudio.Core.Sessions;

/// <summary>
/// Result of <c>wsb list</c>: the reported sessions, or an error when the CLI failed
/// or its output could not be parsed.
/// </summary>
/// <param name="Sessions">Sessions reported by the CLI; empty on failure.</param>
/// <param name="ErrorMessage">Why the listing failed, when it did.</param>
public sealed record WsbListResult(IReadOnlyList<SandboxSession> Sessions, string? ErrorMessage)
{
    /// <summary>Factory for a successful result.</summary>
    public static WsbListResult Success(IReadOnlyList<SandboxSession> sessions) => new(sessions, null);

    /// <summary>Factory for a failed result.</summary>
    public static WsbListResult Failure(string errorMessage) => new([], errorMessage);
}
