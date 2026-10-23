namespace SandboxStudio.Core.Sessions;

/// <summary>
/// Result of <c>wsb exec</c>: the command's exit code inside the sandbox.
/// Process output cannot be captured by the CLI.
/// </summary>
/// <param name="IsSuccess">Whether the command ran.</param>
/// <param name="ExitCode">The in-sandbox process exit code; <see langword="null"/> when the command never ran.</param>
/// <param name="ErrorMessage">Why the command failed, when it did.</param>
public sealed record WsbExecResult(bool IsSuccess, int? ExitCode, string? ErrorMessage)
{
    /// <summary>Factory for a command that ran, regardless of its exit code.</summary>
    public static WsbExecResult Ran(int exitCode) => new(true, exitCode, null);

    /// <summary>Factory for a command that never ran.</summary>
    public static WsbExecResult Failure(string errorMessage) => new(false, null, errorMessage);
}
