using System.Diagnostics;

namespace SandboxStudio.Core.Sessions;

/// <summary>
/// Outcome of a single <c>wsb.exe</c> process invocation.
/// </summary>
/// <param name="ExitCode">Process exit code; -1 when the invocation timed out or failed to start.</param>
/// <param name="StdOut">Captured standard output; empty when capture was not requested.</param>
/// <param name="StdErr">Captured standard error; empty when capture was not requested.</param>
public sealed record WsbInvocationResult(int ExitCode, string StdOut, string StdErr);

/// <summary>
/// Runs a configured <c>wsb.exe</c> process. <paramref name="captureOutput"/> requests
/// stdout/stderr capture; it must be <see langword="false"/> for commands like
/// <c>connect</c> whose window lifetime would otherwise hold the pipes open.
/// </summary>
/// <param name="startInfo">Fully configured <c>wsb.exe</c> start info.</param>
/// <param name="captureOutput">Whether the caller asked for output capture.</param>
/// <param name="cancellationToken">Cancels the wait for the process.</param>
/// <returns>The process exit code and any captured output.</returns>
public delegate Task<WsbInvocationResult> WsbInvoker(
    ProcessStartInfo startInfo,
    bool captureOutput,
    CancellationToken cancellationToken);
