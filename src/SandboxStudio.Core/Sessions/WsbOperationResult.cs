namespace SandboxStudio.Core.Sessions;

/// <summary>
/// Result of a state-changing <c>wsb</c> command such as <c>stop</c> or <c>connect</c>.
/// </summary>
/// <param name="IsSuccess">Whether the command exited successfully.</param>
/// <param name="ErrorMessage">Why the command failed, when it did.</param>
public sealed record WsbOperationResult(bool IsSuccess, string? ErrorMessage)
{
    /// <summary>Factory for a successful result.</summary>
    public static WsbOperationResult Success() => new(true, null);

    /// <summary>Factory for a failed result.</summary>
    public static WsbOperationResult Failure(string errorMessage) => new(false, errorMessage);
}
