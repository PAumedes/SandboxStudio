using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using SandboxStudio.Core.Capabilities;

namespace SandboxStudio.Core.Sessions;

/// <summary>
/// <see cref="ISandboxSessionManager"/> backed by the inbox <c>wsb.exe</c> CLI,
/// invoking it with <c>--raw</c> and parsing the JSON responses.
/// </summary>
public sealed class WsbSessionManager : ISandboxSessionManager
{
    /// <summary>
    /// Maximum serialized configuration length passed inline to <c>wsb start</c>;
    /// the CreateProcess command line caps near 32K and argument escaping inflates quotes.
    /// </summary>
    public const int MaxInlineConfigChars = 28_000;

    private static readonly TimeSpan InvocationTimeout = TimeSpan.FromSeconds(30);

    // Provisioning a sandbox session can take far longer than list/stop.
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(90);

    // After wsb.exe exits, its own output is already flushed; this bounds the drain
    // in case a spawned descendant still holds the redirected pipes open.
    private static readonly TimeSpan DrainTimeout = TimeSpan.FromSeconds(3);

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

        var invocation = await InvokeAsync(InvocationTimeout, captureOutput: true, cancellationToken, "list");
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
    public async Task<WsbStartResult> StartAsync(string configurationXml, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationXml);
        if (!IsSupported)
        {
            return WsbStartResult.Failure("The Windows Sandbox CLI is not available on this host.");
        }

        if (configurationXml.Length > MaxInlineConfigChars)
        {
            return WsbStartResult.Failure(
                "The configuration is too large for a wsb start command line.",
                cliNeverStarted: true);
        }

        var invocation = await InvokeAsync(StartTimeout, captureOutput: true, cancellationToken, "start", "--config", configurationXml);
        if (invocation.ExitCode != 0)
        {
            return WsbStartResult.Failure(
                DescribeFailure("start", invocation),
                cliNeverStarted: !invocation.Spawned);
        }

        try
        {
            var response = JsonSerializer.Deserialize<StartResponseDto>(invocation.StdOut, JsonOptions);
            return response?.Id is { } id && id != Guid.Empty
                ? WsbStartResult.Success(id)
                : WsbStartResult.Failure("The Windows Sandbox CLI did not report a sandbox ID.");
        }
        catch (JsonException)
        {
            return WsbStartResult.Failure("The Windows Sandbox CLI returned output that could not be parsed.");
        }
    }

    /// <inheritdoc/>
    public async Task<WsbExecResult> ExecAsync(
        Guid id,
        string command,
        WsbRunAs runAs,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        if (!IsSupported)
        {
            return WsbExecResult.Failure("The Windows Sandbox CLI is not available on this host.");
        }

        var arguments = new List<string>
        {
            "exec", "--id", id.ToString(), "--command", command, "--run-as", runAs.ToString()
        };
        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            arguments.AddRange(["--working-directory", workingDirectory]);
        }

        var invocation = await InvokeAsync(InvocationTimeout, captureOutput: true, cancellationToken, [.. arguments]);
        if (invocation.ExitCode != 0)
        {
            return WsbExecResult.Failure(DescribeFailure("exec", invocation));
        }

        try
        {
            var response = JsonSerializer.Deserialize<ExecResponseDto>(invocation.StdOut, JsonOptions);
            return response?.ExitCode is { } exitCode
                ? WsbExecResult.Ran(exitCode)
                : WsbExecResult.Failure("The Windows Sandbox CLI did not report an exit code.");
        }
        catch (JsonException)
        {
            return WsbExecResult.Failure("The Windows Sandbox CLI returned output that could not be parsed.");
        }
    }

    /// <inheritdoc/>
    public async Task<WsbIpResult> GetIpAddressAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!IsSupported)
        {
            return WsbIpResult.Failure("The Windows Sandbox CLI is not available on this host.");
        }

        var invocation = await InvokeAsync(InvocationTimeout, captureOutput: true, cancellationToken, "ip", "--id", id.ToString());
        if (invocation.ExitCode != 0)
        {
            return WsbIpResult.Failure(DescribeFailure("ip", invocation));
        }

        try
        {
            var response = JsonSerializer.Deserialize<IpResponseDto>(invocation.StdOut, JsonOptions);
            var addresses = response?.Networks
                ?.Select(network => IPAddress.TryParse(network.IpV4Address, out var address) ? address : null)
                .Where(address => address is not null)
                .Cast<IPAddress>()
                .ToList() ?? [];

            return WsbIpResult.Success(addresses);
        }
        catch (JsonException)
        {
            return WsbIpResult.Failure("The Windows Sandbox CLI returned output that could not be parsed.");
        }
    }

    /// <inheritdoc/>
    public async Task<WsbOperationResult> ShareAsync(
        Guid id,
        string hostPath,
        string sandboxPath,
        bool allowWrite = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(sandboxPath);
        if (!IsSupported)
        {
            return WsbOperationResult.Failure("The Windows Sandbox CLI is not available on this host.");
        }

        var arguments = new List<string>
        {
            "share", "--id", id.ToString(), "--host-path", hostPath, "--sandbox-path", sandboxPath
        };
        if (allowWrite)
        {
            arguments.Add("--allow-write");
        }

        var invocation = await InvokeAsync(InvocationTimeout, captureOutput: true, cancellationToken, [.. arguments]);
        return invocation.ExitCode == 0
            ? WsbOperationResult.Success()
            : WsbOperationResult.Failure(DescribeFailure("share", invocation));
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

        var invocation = await InvokeAsync(InvocationTimeout, captureOutput, cancellationToken, verb, "--id", id.ToString());
        return invocation.ExitCode == 0
            ? WsbOperationResult.Success()
            : WsbOperationResult.Failure(DescribeFailure(verb, invocation));
    }

    private async Task<WsbInvocationResult> InvokeAsync(
        TimeSpan timeout,
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
            return await _invoker(startInfo, captureOutput, timeout, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // An invoker that throws never ran the CLI.
            return new WsbInvocationResult(-1, string.Empty, ex.Message, Spawned: false);
        }
    }

    private static async Task<WsbInvocationResult> InvokeProcessAsync(
        ProcessStartInfo startInfo,
        bool captureOutput,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the Windows Sandbox CLI.");

        // Pumps append under a lock so partially drained output is still usable
        // if a spawned descendant holds the pipes open past wsb.exe's exit.
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var pumps = new List<Task>(capacity: 2);
        if (startInfo.RedirectStandardOutput)
        {
            pumps.Add(PumpAsync(process.StandardOutput, stdout));
        }

        if (startInfo.RedirectStandardError)
        {
            pumps.Add(PumpAsync(process.StandardError, stderr));
        }

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill();
                }
                catch
                {
                    // The invocation is already being reported as timed out.
                }

                return new WsbInvocationResult(
                    -1,
                    Drained(stdout),
                    Drained(stderr) + "The Windows Sandbox CLI did not respond in time; a session may have been started — check Sessions.");
            }
        }

        // The process exited — its own output is flushed. Bound the drain in case a
        // descendant keeps the pipes open.
        try
        {
            await Task.WhenAll(pumps).WaitAsync(DrainTimeout);
        }
        catch (TimeoutException)
        {
        }

        return new WsbInvocationResult(process.ExitCode, Drained(stdout), Drained(stderr));

        static string Drained(StringBuilder buffer)
        {
            lock (buffer)
            {
                return buffer.ToString();
            }
        }

        static async Task PumpAsync(StreamReader reader, StringBuilder buffer)
        {
            var chunk = new char[4096];
            try
            {
                int read;
                while ((read = await reader.ReadAsync(chunk)) > 0)
                {
                    lock (buffer)
                    {
                        buffer.Append(chunk, 0, read);
                    }
                }
            }
            catch (Exception)
            {
                // The process or its pipes are being torn down; keep what was read.
            }
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

    private sealed class StartResponseDto
    {
        public Guid Id { get; set; }
    }

    private sealed class ExecResponseDto
    {
        public int? ExitCode { get; set; }
    }

    private sealed class IpResponseDto
    {
        public List<IpNetworkDto>? Networks { get; set; }
    }

    private sealed class IpNetworkDto
    {
        public string? IpV4Address { get; set; }
    }

    private sealed class EnvironmentDto
    {
        public Guid Id { get; set; }

        public string? Status { get; set; }

        public string? State { get; set; }

        public string? Uptime { get; set; }
    }
}
