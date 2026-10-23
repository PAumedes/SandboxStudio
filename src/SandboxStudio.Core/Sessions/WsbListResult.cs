namespace SandboxStudio.Core.Sessions;

/// <summary>
/// Result of <c>wsb list</c>: the reported sessions, or an error when the CLI failed
/// or its output could not be parsed.
/// </summary>
/// <param name="Sessions">Sessions reported by the CLI; empty on failure.</param>
/// <param name="ErrorMessage">Why the listing failed, when it did.</param>
/// <param name="ErrorKey">Optional <see cref="Localization.MessageKeys"/> entry for localized display.</param>
/// <param name="ErrorArgs">Optional format arguments paired with <paramref name="ErrorKey"/>.</param>
public sealed record WsbListResult(IReadOnlyList<SandboxSession> Sessions, string? ErrorMessage, string? ErrorKey = null, IReadOnlyList<object?>? ErrorArgs = null)
{
    /// <summary>Factory for a successful result.</summary>
    public static WsbListResult Success(IReadOnlyList<SandboxSession> sessions) => new(sessions, null);

    /// <summary>Factory for a failed result.</summary>
    public static WsbListResult Failure(string errorMessage, string? errorKey = null, IReadOnlyList<object?>? errorArgs = null) => new([], errorMessage, errorKey, errorArgs);
}
