namespace SandboxStudio.Core.Sessions;

/// <summary>
/// Result of <c>wsb exec</c>: the command's exit code inside the sandbox.
/// Process output cannot be captured by the CLI.
/// </summary>
/// <param name="IsSuccess">Whether the command ran.</param>
/// <param name="ExitCode">The in-sandbox process exit code; <see langword="null"/> when the command never ran.</param>
/// <param name="ErrorMessage">Why the command failed, when it did.</param>
/// <param name="ErrorKey">Optional <see cref="Localization.MessageKeys"/> entry for localized display.</param>
/// <param name="ErrorArgs">Optional format arguments paired with <paramref name="ErrorKey"/>.</param>
public sealed record WsbExecResult(bool IsSuccess, int? ExitCode, string? ErrorMessage, string? ErrorKey = null, IReadOnlyList<object?>? ErrorArgs = null)
{
    /// <summary>Factory for a command that ran, regardless of its exit code.</summary>
    public static WsbExecResult Ran(int exitCode) => new(true, exitCode, null);

    /// <summary>Factory for a command that never ran.</summary>
    public static WsbExecResult Failure(string errorMessage, string? errorKey = null, IReadOnlyList<object?>? errorArgs = null) => new(false, null, errorMessage, errorKey, errorArgs);
}
