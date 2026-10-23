namespace SandboxStudio.Core.Execution;

/// <summary>
/// Result returned after attempting to launch Windows Sandbox.
/// </summary>
public sealed record SandboxLaunchResult(
    bool IsSuccess,
    int? ProcessId,
    string? WsbFilePath,
    string? ErrorMessage,
    Guid? SessionId = null
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
    public static SandboxLaunchResult Failure(string errorMessage) =>
        new(false, null, null, errorMessage);
}
