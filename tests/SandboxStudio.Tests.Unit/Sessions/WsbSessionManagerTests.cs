using System.Diagnostics;
using FluentAssertions;
using Moq;
using SandboxStudio.Core.Capabilities;
using SandboxStudio.Core.Sessions;
using Xunit;

namespace SandboxStudio.Tests.Unit.Sessions;

public sealed class WsbSessionManagerTests
{
    private readonly Mock<IHostCapabilities> _mockCapabilities = new();
    private readonly List<(ProcessStartInfo StartInfo, bool CaptureOutput)> _invocations = new();
    private WsbInvocationResult _nextResult = new(0, string.Empty, string.Empty);

    private WsbSessionManager CreateSut(bool wsbExeExists = true)
    {
        return new WsbSessionManager(
            _mockCapabilities.Object,
            wsbExecutableExists: () => wsbExeExists,
            invoker: CaptureInvoker);
    }

    private Task<WsbInvocationResult> CaptureInvoker(
        ProcessStartInfo startInfo,
        bool captureOutput,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        _invocations.Add((startInfo, captureOutput));
        return Task.FromResult(_nextResult);
    }

    private void ArrangeAvailability(FeatureStatus status)
    {
        _mockCapabilities
            .Setup(c => c.GetAvailability(SandboxFeature.WsbCli))
            .Returns(new FeatureAvailability(status));
    }

    [Fact]
    public void IsSupported_WhenCliFeatureBlockedByOldBuild_ReturnsFalse()
    {
        // Arrange
        ArrangeAvailability(FeatureStatus.RequiresNewerWindows);
        var sut = CreateSut();

        // Act & Assert
        sut.IsSupported.Should().BeFalse();
    }

    [Fact]
    public void IsSupported_WhenCliExecutableMissing_ReturnsFalse()
    {
        // Arrange
        ArrangeAvailability(FeatureStatus.Available);
        var sut = CreateSut(wsbExeExists: false);

        // Act & Assert
        sut.IsSupported.Should().BeFalse();
    }

    [Fact]
    public void IsSupported_WhenFeatureAvailableAndExePresent_ReturnsTrue()
    {
        // Arrange
        ArrangeAvailability(FeatureStatus.Available);
        var sut = CreateSut();

        // Act & Assert
        sut.IsSupported.Should().BeTrue();
    }

    [Fact]
    public async Task ListAsync_WhenNotSupported_ReturnsEmptyWithoutInvokingCli()
    {
        // Arrange
        ArrangeAvailability(FeatureStatus.RequiresNewerWindows);
        var sut = CreateSut();

        // Act
        var result = await sut.ListAsync();

        // Assert
        result.Sessions.Should().BeEmpty();
        _invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task ListAsync_ParsesSessionsFromRawJson()
    {
        // Arrange
        ArrangeAvailability(FeatureStatus.Available);
        var id = Guid.NewGuid();
        _nextResult = new(0, $$"""
            {"WindowsSandboxEnvironments":[{"Id":"{{id}}","Status":"running","Uptime":"0:05:12"}]}
            """, string.Empty);
        var sut = CreateSut();

        // Act
        var result = await sut.ListAsync();

        // Assert
        result.ErrorMessage.Should().BeNull();
        result.Sessions.Should().ContainSingle();
        result.Sessions[0].Id.Should().Be(id);
        result.Sessions[0].State.Should().Be(SandboxSessionState.Running);
        result.Sessions[0].Uptime.Should().Be(TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(12)));
    }

    [Fact]
    public async Task ListAsync_WhenEnvironmentFieldsAreMissing_ToleratesThem()
    {
        // Arrange - --raw output may carry only the id on some builds
        ArrangeAvailability(FeatureStatus.Available);
        var id = Guid.NewGuid();
        _nextResult = new(0, $$"""{"WindowsSandboxEnvironments":[{"Id":"{{id}}"}]}""", string.Empty);
        var sut = CreateSut();

        // Act
        var result = await sut.ListAsync();

        // Assert
        result.Sessions.Should().ContainSingle();
        result.Sessions[0].State.Should().Be(SandboxSessionState.Unknown);
        result.Sessions[0].Uptime.Should().BeNull();
    }

    [Fact]
    public async Task ListAsync_WhenEnvironmentsArrayAbsent_ReturnsEmpty()
    {
        // Arrange
        ArrangeAvailability(FeatureStatus.Available);
        _nextResult = new(0, "{}", string.Empty);
        var sut = CreateSut();

        // Act
        var result = await sut.ListAsync();

        // Assert
        result.Sessions.Should().BeEmpty();
    }

    [Fact]
    public async Task ListAsync_WhenOutputIsNotJson_ReturnsError()
    {
        // Arrange
        ArrangeAvailability(FeatureStatus.Available);
        _nextResult = new(0, "not json", string.Empty);
        var sut = CreateSut();

        // Act
        var result = await sut.ListAsync();

        // Assert
        result.Sessions.Should().BeEmpty();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ListAsync_WhenCliFails_ReturnsErrorWithStderr()
    {
        // Arrange
        ArrangeAvailability(FeatureStatus.Available);
        _nextResult = new(1, string.Empty, "access denied");
        var sut = CreateSut();

        // Act
        var result = await sut.ListAsync();

        // Assert
        result.Sessions.Should().BeEmpty();
        result.ErrorMessage.Should().Contain("access denied");
    }

    [Fact]
    public async Task ListAsync_InvokesCliWithRawFlagBeforeListVerb()
    {
        // Arrange
        ArrangeAvailability(FeatureStatus.Available);
        _nextResult = new(0, "{}", string.Empty);
        var sut = CreateSut();

        // Act
        await sut.ListAsync();

        // Assert
        _invocations.Should().ContainSingle();
        _invocations[0].StartInfo.ArgumentList.Should().Equal("--raw", "list");
        _invocations[0].CaptureOutput.Should().BeTrue();
    }

    [Fact]
    public async Task StopAsync_PassesSessionIdAndReportsSuccess()
    {
        // Arrange
        ArrangeAvailability(FeatureStatus.Available);
        var id = Guid.NewGuid();
        var sut = CreateSut();

        // Act
        var result = await sut.StopAsync(id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _invocations.Should().ContainSingle();
        _invocations[0].StartInfo.ArgumentList.Should().Equal("--raw", "stop", "--id", id.ToString());
    }

    [Fact]
    public async Task StopAsync_WhenCliFails_ReturnsFailure()
    {
        // Arrange
        ArrangeAvailability(FeatureStatus.Available);
        _nextResult = new(3, string.Empty, "no such sandbox");
        var sut = CreateSut();

        // Act
        var result = await sut.StopAsync(Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("no such sandbox");
    }

    [Fact]
    public async Task StartAsync_PassesConfigInlineAndReturnsAssignedId()
    {
        // Arrange
        ArrangeAvailability(FeatureStatus.Available);
        var id = Guid.NewGuid();
        _nextResult = new(0, $$"""{"Id":"{{id}}"}""", string.Empty);
        var sut = CreateSut();

        // Act
        var result = await sut.StartAsync("<Configuration><Networking>Disable</Networking></Configuration>");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.SessionId.Should().Be(id);
        _invocations.Should().ContainSingle();
        _invocations[0].StartInfo.ArgumentList.Should().Equal(
            "--raw", "start", "--config", "<Configuration><Networking>Disable</Networking></Configuration>");
    }

    [Fact]
    public async Task StartAsync_WhenNotSupported_ReturnsFailureWithoutInvokingCli()
    {
        // Arrange
        ArrangeAvailability(FeatureStatus.RequiresNewerWindows);
        var sut = CreateSut();

        // Act
        var result = await sut.StartAsync("<Configuration />");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
        _invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task StartAsync_WhenCliFails_ReturnsFailure()
    {
        // Arrange
        ArrangeAvailability(FeatureStatus.Available);
        _nextResult = new(1, string.Empty, "configuration rejected");
        var sut = CreateSut();

        // Act
        var result = await sut.StartAsync("<Configuration />");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("configuration rejected");
    }

    [Fact]
    public async Task StartAsync_WhenResponseLacksId_ReturnsFailure()
    {
        // Arrange
        ArrangeAvailability(FeatureStatus.Available);
        _nextResult = new(0, "{}", string.Empty);
        var sut = CreateSut();

        // Act
        var result = await sut.StartAsync("<Configuration />");

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task StartAsync_WhenOutputIsNotJson_ReturnsFailure()
    {
        // Arrange
        ArrangeAvailability(FeatureStatus.Available);
        _nextResult = new(0, "started!", string.Empty);
        var sut = CreateSut();

        // Act
        var result = await sut.StartAsync("<Configuration />");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task StartAsync_WhenConfigExceedsInlineLimit_NeverInvokesCliAndMarksFallbackSafe()
    {
        // Arrange - the CreateProcess command line caps near 32K; oversized
        // configs must fall back rather than fail the launch
        ArrangeAvailability(FeatureStatus.Available);
        var sut = CreateSut();
        var oversizedXml = new string('x', WsbSessionManager.MaxInlineConfigChars + 1);

        // Act
        var result = await sut.StartAsync(oversizedXml);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.CliNeverStarted.Should().BeTrue();
        _invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task StartAsync_WhenCliNeverSpawned_PropagatesFallbackSafety()
    {
        // Arrange - a spawn failure means nothing reached wsb.exe
        ArrangeAvailability(FeatureStatus.Available);
        _nextResult = new(-1, string.Empty, "The system cannot find the file specified.", Spawned: false);
        var sut = CreateSut();

        // Act
        var result = await sut.StartAsync("<Configuration />");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.CliNeverStarted.Should().BeTrue();
    }

    [Fact]
    public async Task StartAsync_WhenCliRanAndFailed_IsNotFallbackSafe()
    {
        // Arrange - a non-zero exit from a running CLI may have started a session
        ArrangeAvailability(FeatureStatus.Available);
        _nextResult = new(1, string.Empty, "rejected");
        var sut = CreateSut();

        // Act
        var result = await sut.StartAsync("<Configuration />");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.CliNeverStarted.Should().BeFalse();
    }

    [Fact]
    public async Task ExecAsync_PassesCommandRunAsAndWorkingDirectory()
    {
        // Arrange
        ArrangeAvailability(FeatureStatus.Available);
        var id = Guid.NewGuid();
        _nextResult = new(0, """{"ExitCode":42}""", string.Empty);
        var sut = CreateSut();

        // Act
        var result = await sut.ExecAsync(id, "app.exe", WsbRunAs.System, @"C:\Tools");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ExitCode.Should().Be(42);
        _invocations.Should().ContainSingle();
        _invocations[0].StartInfo.ArgumentList.Should().Equal(
            "--raw", "exec", "--id", id.ToString(),
            "--command", "app.exe", "--run-as", "System",
            "--working-directory", @"C:\Tools");
    }

    [Fact]
    public async Task ExecAsync_WithoutWorkingDirectory_OmitsTheArgument()
    {
        // Arrange
        ArrangeAvailability(FeatureStatus.Available);
        _nextResult = new(0, """{"ExitCode":0}""", string.Empty);
        var sut = CreateSut();

        // Act
        await sut.ExecAsync(Guid.NewGuid(), "cmd", WsbRunAs.ExistingLogin);

        // Assert
        _invocations[0].StartInfo.ArgumentList.Should().NotContain("--working-directory");
        _invocations[0].StartInfo.ArgumentList.Should().Contain("ExistingLogin");
    }

    [Fact]
    public async Task ExecAsync_WhenNotSupported_ReturnsFailureWithoutInvokingCli()
    {
        // Arrange
        ArrangeAvailability(FeatureStatus.RequiresNewerWindows);
        var sut = CreateSut();

        // Act
        var result = await sut.ExecAsync(Guid.NewGuid(), "cmd", WsbRunAs.System);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ExitCode.Should().BeNull();
        _invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecAsync_WhenCliFails_ReturnsFailure()
    {
        // Arrange
        ArrangeAvailability(FeatureStatus.Available);
        _nextResult = new(1, string.Empty, "no active user session");
        var sut = CreateSut();

        // Act
        var result = await sut.ExecAsync(Guid.NewGuid(), "cmd", WsbRunAs.ExistingLogin);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("no active user session");
    }

    [Fact]
    public async Task ExecAsync_WhenResponseLacksExitCode_ReturnsFailure()
    {
        // Arrange
        ArrangeAvailability(FeatureStatus.Available);
        _nextResult = new(0, "{}", string.Empty);
        var sut = CreateSut();

        // Act
        var result = await sut.ExecAsync(Guid.NewGuid(), "cmd", WsbRunAs.System);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecAsync_WithWhitespaceWorkingDirectory_OmitsTheArgument()
    {
        // Arrange
        ArrangeAvailability(FeatureStatus.Available);
        _nextResult = new(0, """{"ExitCode":0}""", string.Empty);
        var sut = CreateSut();

        // Act
        await sut.ExecAsync(Guid.NewGuid(), "cmd", WsbRunAs.System, "   ");

        // Assert
        _invocations[0].StartInfo.ArgumentList.Should().NotContain("--working-directory");
    }

    [Fact]
    public async Task ExecAsync_WhenExitCodeExplicitlyNull_ReturnsFailure()
    {
        // Arrange
        ArrangeAvailability(FeatureStatus.Available);
        _nextResult = new(0, """{"ExitCode":null}""", string.Empty);
        var sut = CreateSut();

        // Act
        var result = await sut.ExecAsync(Guid.NewGuid(), "cmd", WsbRunAs.System);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ExitCode.Should().BeNull();
    }

    [Fact]
    public async Task ExecAsync_WhenOutputIsNotJson_ReturnsFailure()
    {
        // Arrange
        ArrangeAvailability(FeatureStatus.Available);
        _nextResult = new(0, "done", string.Empty);
        var sut = CreateSut();

        // Act
        var result = await sut.ExecAsync(Guid.NewGuid(), "cmd", WsbRunAs.System);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExecAsync_WhenCommandIsEmpty_Throws()
    {
        // Arrange
        ArrangeAvailability(FeatureStatus.Available);
        var sut = CreateSut();

        // Act
        var act = () => sut.ExecAsync(Guid.NewGuid(), "  ", WsbRunAs.System);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
        _invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task GetIpAddressAsync_WhenAddressIsUnparsable_FiltersItOut()
    {
        // Arrange
        ArrangeAvailability(FeatureStatus.Available);
        _nextResult = new(0,
            """{"Networks":[{"IpV4Address":"not-an-ip"},{"IpV4Address":"10.0.0.5"}]}""",
            string.Empty);
        var sut = CreateSut();

        // Act
        var result = await sut.GetIpAddressAsync(Guid.NewGuid());

        // Assert
        result.Addresses.Should().ContainSingle().Which.ToString().Should().Be("10.0.0.5");
    }

    [Fact]
    public async Task GetIpAddressAsync_ParsesNetworks()
    {
        // Arrange
        ArrangeAvailability(FeatureStatus.Available);
        var id = Guid.NewGuid();
        _nextResult = new(0, """{"Networks":[{"IpV4Address":"192.168.10.20"}]}""", string.Empty);
        var sut = CreateSut();

        // Act
        var result = await sut.GetIpAddressAsync(id);

        // Assert
        result.ErrorMessage.Should().BeNull();
        result.Addresses.Should().ContainSingle();
        result.Addresses[0].ToString().Should().Be("192.168.10.20");
        _invocations[0].StartInfo.ArgumentList.Should().Equal("--raw", "ip", "--id", id.ToString());
    }

    [Fact]
    public async Task GetIpAddressAsync_WhenNoNetworks_ReturnsEmpty()
    {
        // Arrange
        ArrangeAvailability(FeatureStatus.Available);
        _nextResult = new(0, """{"Networks":[]}""", string.Empty);
        var sut = CreateSut();

        // Act
        var result = await sut.GetIpAddressAsync(Guid.NewGuid());

        // Assert
        result.Addresses.Should().BeEmpty();
    }

    [Fact]
    public async Task GetIpAddressAsync_WhenCliFails_ReturnsError()
    {
        // Arrange
        ArrangeAvailability(FeatureStatus.Available);
        _nextResult = new(1, string.Empty, "no such sandbox");
        var sut = CreateSut();

        // Act
        var result = await sut.GetIpAddressAsync(Guid.NewGuid());

        // Assert
        result.ErrorMessage.Should().Contain("no such sandbox");
    }

    [Fact]
    public async Task ConnectAsync_DoesNotCaptureStdout()
    {
        // Arrange - wsb connect returns immediately; capturing stdout would
        // keep the pipe open and block until the remote window closes
        ArrangeAvailability(FeatureStatus.Available);
        var id = Guid.NewGuid();
        var sut = CreateSut();

        // Act
        var result = await sut.ConnectAsync(id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _invocations.Should().ContainSingle();
        _invocations[0].StartInfo.ArgumentList.Should().Equal("--raw", "connect", "--id", id.ToString());
        _invocations[0].CaptureOutput.Should().BeFalse();
    }
}
