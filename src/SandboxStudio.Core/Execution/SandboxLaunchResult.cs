namespace SandboxStudio.Core.Execution;

/// <summary>
/// Result returned after attempting to launch Windows Sandbox.
/// </summary>
public sealed record SandboxLaunchResult(
    bool IsSuccess,
    int? ProcessId,
    string? WsbFilePath,
    string? ErrorMessage
)
{
    /// <summary>
    /// Factory for successful launch result.
    /// </summary>
    public static SandboxLaunchResult Success(int? processId, string wsbFilePath) =>
        new(true, processId, wsbFilePath, null);

    /// <summary>
    /// Factory for failed launch result.
    /// </summary>
    public static SandboxLaunchResult Failure(string errorMessage) =>
        new(false, null, null, errorMessage);
}
