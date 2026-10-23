using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using SandboxStudio.Core.Capabilities;

namespace SandboxStudio.Core.Sessions;

/// <summary>
/// <see cref="ISandboxSessionManager"/> backed by the inbox <c>wsb.exe</c> CLI,
/// invoking it with <c>--raw</c> and parsing the JSON responses.
/// </summary>
public sealed class WsbSessionManager : ISandboxSessionManager
{
    private static readonly TimeSpan InvocationTimeout = TimeSpan.FromSeconds(30);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHostCapabilities _capabilities;
    private readonly Func<bool> _wsbExecutableExists;
    private readonly WsbInvoker _invoker;

    /// <summary>
    /// Initializes a new instance of <see cref="WsbSessionManager"/>.
    /// Probes are injectable to keep the service testable on any machine.
    /// </summary>
    /// <param name="capabilities">Host capability service; the CLI requires Windows 11 24H2+.</param>
    /// <param name="wsbExecutableExists"><c>wsb.exe</c> presence probe.</param>
    /// <param name="invoker">Process runner used for each CLI call.</param>
    public WsbSessionManager(
        IHostCapabilities capabilities,
        Func<bool>? wsbExecutableExists = null,
        WsbInvoker? invoker = null)
    {
        _capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
        _wsbExecutableExists = wsbExecutableExists ?? (() => File.Exists(SandboxHostPaths.WsbExe));
        _invoker = invoker ?? InvokeProcessAsync;
    }

    /// <inheritdoc/>
    public bool IsSupported =>
        _capabilities.GetAvailability(SandboxFeature.WsbCli).Status == FeatureStatus.Available &&
        _wsbExecutableExists();

    /// <inheritdoc/>
    public async Task<WsbListResult> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!IsSupported)
        {
            return WsbListResult.Failure("The Windows Sandbox CLI is not available on this host.");
        }

        var invocation = await InvokeAsync(captureOutput: true, cancellationToken, "list");
        if (invocation.ExitCode != 0)
        {
            return WsbListResult.Failure(DescribeFailure("list", invocation));
        }

        try
        {
            var response = JsonSerializer.Deserialize<ListResponseDto>(invocation.StdOut, JsonOptions);
            var sessions = response?.WindowsSandboxEnvironments
                ?.Where(environment => environment.Id != Guid.Empty)
                .Select(environment => new SandboxSession
                {
                    Id = environment.Id,
                    State = MapState(environment.Status) is var state && state != SandboxSessionState.Unknown
                        ? state
                        : MapState(environment.State),
                    Uptime = TimeSpan.TryParse(environment.Uptime, CultureInfo.InvariantCulture, out var uptime)
                        ? uptime
                        : null
                })
                .ToList() ?? [];

            return WsbListResult.Success(sessions);
        }
        catch (JsonException)
        {
            return WsbListResult.Failure("The Windows Sandbox CLI returned output that could not be parsed.");
        }
    }

    /// <inheritdoc/>
    public async Task<WsbOperationResult> StopAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await RunStateChangingAsync("stop", id, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<WsbOperationResult> ConnectAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // wsb connect returns immediately after opening the remote window; capturing
        // stdout would hold the pipe open until that window closes.
        return await RunStateChangingAsync("connect", id, cancellationToken, captureOutput: false);
    }

    private async Task<WsbOperationResult> RunStateChangingAsync(
        string verb,
        Guid id,
        CancellationToken cancellationToken,
        bool captureOutput = true)
    {
        if (!IsSupported)
        {
            return WsbOperationResult.Failure("The Windows Sandbox CLI is not available on this host.");
        }

        var invocation = await InvokeAsync(captureOutput, cancellationToken, verb, "--id", id.ToString());
        return invocation.ExitCode == 0
            ? WsbOperationResult.Success()
            : WsbOperationResult.Failure(DescribeFailure(verb, invocation));
    }

    private async Task<WsbInvocationResult> InvokeAsync(
        bool captureOutput,
        CancellationToken cancellationToken,
        params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = SandboxHostPaths.WsbExe,
            UseShellExecute = false,
            RedirectStandardOutput = captureOutput,
            // A remote-session window inherits both output pipes, so stderr is
            // only captured alongside stdout.
            RedirectStandardError = captureOutput,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("--raw");
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            return await _invoker(startInfo, captureOutput, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return new WsbInvocationResult(-1, string.Empty, ex.Message);
        }
    }

    private static async Task<WsbInvocationResult> InvokeProcessAsync(
        ProcessStartInfo startInfo,
        bool captureOutput,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(InvocationTimeout);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the Windows Sandbox CLI.");
        try
        {
            var stdoutTask = startInfo.RedirectStandardOutput
                ? process.StandardOutput.ReadToEndAsync(timeout.Token)
                : Task.FromResult(string.Empty);
            var stderrTask = startInfo.RedirectStandardError
                ? process.StandardError.ReadToEndAsync(timeout.Token)
                : Task.FromResult(string.Empty);
            await process.WaitForExitAsync(timeout.Token);
            return new WsbInvocationResult(process.ExitCode, await stdoutTask, await stderrTask);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            if (process.HasExited)
            {
                // The process exited between WaitForExitAsync and the stream
                // awaits; report its real exit code rather than a timeout.
                return new WsbInvocationResult(process.ExitCode, string.Empty, string.Empty);
            }

            try
            {
                process.Kill();
            }
            catch
            {
                // The invocation is already being reported as timed out.
            }

            return new WsbInvocationResult(-1, string.Empty, "The Windows Sandbox CLI did not respond in time.");
        }
    }

    private static string DescribeFailure(string verb, WsbInvocationResult invocation) =>
        string.IsNullOrWhiteSpace(invocation.StdErr)
            ? $"wsb {verb} failed with exit code {invocation.ExitCode}."
            : $"wsb {verb} failed: {invocation.StdErr.Trim()}";

    private static SandboxSessionState MapState(string? status) =>
        status switch
        {
            not null when status.Equals("running", StringComparison.OrdinalIgnoreCase) => SandboxSessionState.Running,
            not null when status.Equals("stopped", StringComparison.OrdinalIgnoreCase) => SandboxSessionState.Stopped,
            _ => SandboxSessionState.Unknown
        };

    private sealed class ListResponseDto
    {
        public List<EnvironmentDto>? WindowsSandboxEnvironments { get; set; }
    }

    private sealed class EnvironmentDto
    {
        public Guid Id { get; set; }

        public string? Status { get; set; }

        public string? State { get; set; }

        public string? Uptime { get; set; }
    }
}
