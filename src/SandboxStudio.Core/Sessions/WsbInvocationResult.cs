using System.Diagnostics;

namespace SandboxStudio.Core.Sessions;

/// <summary>
/// Outcome of a single <c>wsb.exe</c> process invocation.
/// </summary>
/// <param name="ExitCode">Process exit code; -1 when the invocation timed out or failed to start.</param>
/// <param name="StdOut">Captured standard output; empty when capture was not requested.</param>
/// <param name="StdErr">Captured standard error; empty when capture was not requested.</param>
/// <param name="Spawned">
/// Whether the CLI process actually ran. <see langword="false"/> means the command
/// provably never reached <c>wsb.exe</c>, so falling back to another launch path is safe.
/// </param>
/// <param name="TimedOut">Whether the invocation was killed after exceeding its timeout.</param>
public sealed record WsbInvocationResult(int ExitCode, string StdOut, string StdErr, bool Spawned = true, bool TimedOut = false);

/// <summary>
/// Runs a configured <c>wsb.exe</c> process. <paramref name="captureOutput"/> requests
/// stdout/stderr capture; it must be <see langword="false"/> for commands like
/// <c>connect</c> whose window lifetime would otherwise hold the pipes open.
/// </summary>
/// <param name="startInfo">Fully configured <c>wsb.exe</c> start info.</param>
/// <param name="captureOutput">Whether the caller asked for output capture.</param>
/// <param name="timeout">Maximum time to wait for the process to exit.</param>
/// <param name="cancellationToken">Cancels the wait for the process.</param>
/// <returns>The process exit code and any captured output.</returns>
public delegate Task<WsbInvocationResult> WsbInvoker(
    ProcessStartInfo startInfo,
    bool captureOutput,
    TimeSpan timeout,
    CancellationToken cancellationToken);
