namespace SandboxStudio.Core.Sessions;

/// <summary>
/// Result of a state-changing <c>wsb</c> command such as <c>stop</c> or <c>connect</c>.
/// </summary>
/// <param name="IsSuccess">Whether the command exited successfully.</param>
/// <param name="ErrorMessage">Why the command failed, when it did.</param>
/// <param name="ErrorKey">Optional <see cref="Localization.MessageKeys"/> entry for localized display.</param>
/// <param name="ErrorArgs">Optional format arguments paired with <paramref name="ErrorKey"/>.</param>
public sealed record WsbOperationResult(bool IsSuccess, string? ErrorMessage, string? ErrorKey = null, IReadOnlyList<object?>? ErrorArgs = null)
{
    /// <summary>Factory for a successful result.</summary>
    public static WsbOperationResult Success() => new(true, null);

    /// <summary>Factory for a failed result.</summary>
    public static WsbOperationResult Failure(string errorMessage, string? errorKey = null, IReadOnlyList<object?>? errorArgs = null) => new(false, errorMessage, errorKey, errorArgs);
}
