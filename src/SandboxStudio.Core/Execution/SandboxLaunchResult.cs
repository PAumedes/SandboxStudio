namespace SandboxStudio.Core.Execution;

/// <summary>
/// Result returned after attempting to launch Windows Sandbox.
/// </summary>
/// <param name="IsSuccess">Whether the launch attempt succeeded.</param>
/// <param name="ProcessId">Process ID of the launched sandbox, when known.</param>
/// <param name="WsbFilePath">Temporary <c>.wsb</c> file used for the launch, when applicable.</param>
/// <param name="ErrorMessage">Human-readable English fallback error text.</param>
/// <param name="SessionId">Sandbox session ID when launched through the <c>wsb</c> CLI.</param>
/// <param name="ErrorKey">Optional <see cref="Localization.MessageKeys"/> entry for localized display.</param>
/// <param name="ErrorArgs">Optional format arguments paired with <paramref name="ErrorKey"/>.</param>
public sealed record SandboxLaunchResult(
    bool IsSuccess,
    int? ProcessId,
    string? WsbFilePath,
    string? ErrorMessage,
    Guid? SessionId = null,
    string? ErrorKey = null,
    IReadOnlyList<object?>? ErrorArgs = null
)
{
    /// <summary>
    /// Factory for successful launch result.
    /// </summary>
    public static SandboxLaunchResult Success(int? processId, string wsbFilePath) =>
        new(true, processId, wsbFilePath, null);

    /// <summary>
    /// Factory for a launch started through the <c>wsb</c> CLI, which reports a sandbox ID.
    /// </summary>
    public static SandboxLaunchResult CliSuccess(Guid sessionId) =>
        new(true, null, null, null, sessionId);

    /// <summary>
    /// Factory for failed launch result.
    /// </summary>
    public static SandboxLaunchResult Failure(
        string errorMessage,
        string? errorKey = null,
        IReadOnlyList<object?>? errorArgs = null) =>
        new(false, null, null, errorMessage, null, errorKey, errorArgs);
}
