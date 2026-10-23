using FluentAssertions;
using Moq;
using SandboxStudio.Core.Execution;
using SandboxStudio.Core.Localization;
using SandboxStudio.Core.Models;
using SandboxStudio.Core.Serialization;
using SandboxStudio.Core.Sessions;
using SandboxStudio.Core.Storage;
using SandboxStudio.Core.Validation;
using Xunit;

namespace SandboxStudio.Tests.Unit.Execution;

public sealed class SandboxLauncherTests : IDisposable
{
    private readonly Mock<IWsbSerializer> _mockSerializer = new();
    private readonly Mock<IWsbValidator> _mockValidator = new();
    private readonly Mock<IProfileRepository> _mockRepository = new();
    private readonly string _tempDir;
    private readonly SandboxLauncher _sut;

    public SandboxLauncherTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SandboxStudioLauncherTests", Guid.NewGuid().ToString("N"));
        _sut = new SandboxLauncher(_mockSerializer.Object, _mockValidator.Object, _mockRepository.Object, _tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task LaunchAsync_WhenValidationFails_ReturnsFailureWithoutWritingOrExecuting()
    {
        // Arrange
        var invalidConfig = new WsbConfiguration();
        var failureResult = new ValidationResult();
        failureResult.AddError("MemoryInMB", "Invalid memory");

        _mockValidator.Setup(v => v.Validate(invalidConfig)).Returns(failureResult);

        // Act
        var result = await _sut.LaunchAsync(invalidConfig);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid memory");
        _mockSerializer.Verify(s => s.SerializeToFileAsync(It.IsAny<WsbConfiguration>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void IsSandboxAvailable_ReturnsConsistentBooleanBasedOnSystemExecutable()
    {
        // Act
        var isAvailable = _sut.IsSandboxAvailable();

        // Assert - Verifies path check against %SystemRoot%\System32\WindowsSandbox.exe
        var expected = File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsSandbox.exe"));
        isAvailable.Should().Be(expected);
    }

    [Fact]
    public async Task LaunchAsync_WhenSandboxIsUnavailable_ReturnsFailureWithoutWriting()
    {
        // Arrange
        var sut = new SandboxLauncher(
            _mockSerializer.Object,
            _mockValidator.Object,
            _mockRepository.Object,
            _tempDir,
            sandboxAvailability: () => false);

        _mockValidator.Setup(v => v.Validate(It.IsAny<WsbConfiguration>())).Returns(new ValidationResult());

        // Act
        var result = await sut.LaunchAsync(new WsbConfiguration());

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not installed or enabled");
        _mockSerializer.Verify(s => s.SerializeToFileAsync(It.IsAny<WsbConfiguration>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LaunchAsync_WhenSerializationIsCancelled_PropagatesCancellation()
    {
        // Arrange
        var sut = new SandboxLauncher(
            _mockSerializer.Object,
            _mockValidator.Object,
            _mockRepository.Object,
            _tempDir,
            sandboxAvailability: () => true);

        _mockValidator.Setup(v => v.Validate(It.IsAny<WsbConfiguration>())).Returns(new ValidationResult());
        _mockSerializer
            .Setup(s => s.SerializeToFileAsync(It.IsAny<WsbConfiguration>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var act = () => sut.LaunchAsync(new WsbConfiguration());

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task LaunchProfileAsync_WhenLaunchSucceeds_SetsLastLaunchedAtAndPersistsProfile()
    {
        // Arrange
        var profile = new SandboxProfile();
        var sut = new SandboxLauncher(
            _mockSerializer.Object,
            _mockValidator.Object,
            _mockRepository.Object,
            _tempDir,
            processStarter: _ => null,
            sandboxAvailability: () => true);

        _mockValidator.Setup(v => v.Validate(profile.Configuration)).Returns(new ValidationResult());

        // Act
        var result = await sut.LaunchProfileAsync(profile);

        // Assert
        result.IsSuccess.Should().BeTrue();
        profile.LastLaunchedAt.Should().NotBeNull();
        _mockRepository.Verify(r => r.SaveAsync(profile, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LaunchProfileAsync_WhenLaunchValidationFails_DoesNotPersistLastLaunchedAt()
    {
        // Arrange
        var profile = new SandboxProfile
        {
            Configuration = new WsbConfiguration { MemoryInMB = 100 }
        };

        var failureResult = new ValidationResult();
        failureResult.AddError("MemoryInMB", "Below minimum");
        _mockValidator.Setup(v => v.Validate(profile.Configuration)).Returns(failureResult);

        // Act
        var result = await _sut.LaunchProfileAsync(profile);

        // Assert
        result.IsSuccess.Should().BeFalse();
        profile.LastLaunchedAt.Should().BeNull();
        _mockRepository.Verify(r => r.SaveAsync(It.IsAny<SandboxProfile>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LaunchAsync_WhenSessionManagerSupported_StartsViaCliWithoutWritingFile()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var mockSessions = new Mock<ISandboxSessionManager>();
        mockSessions.Setup(s => s.IsSupported).Returns(true);
        mockSessions
            .Setup(s => s.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WsbStartResult.Success(sessionId));
        _mockSerializer.Setup(s => s.Serialize(It.IsAny<WsbConfiguration>())).Returns("<Configuration />");
        _mockValidator.Setup(v => v.Validate(It.IsAny<WsbConfiguration>())).Returns(new ValidationResult());

        var processStarterInvoked = false;
        var sut = new SandboxLauncher(
            _mockSerializer.Object,
            _mockValidator.Object,
            _mockRepository.Object,
            _tempDir,
            processStarter: _ => { processStarterInvoked = true; return null; },
            sandboxAvailability: () => false,
            sessionManager: mockSessions.Object);

        // Act
        var result = await sut.LaunchAsync(new WsbConfiguration());

        // Assert - the CLI path skips both the availability probe and the temp .wsb file
        result.IsSuccess.Should().BeTrue();
        result.SessionId.Should().Be(sessionId);
        processStarterInvoked.Should().BeFalse();
        _mockSerializer.Verify(
            s => s.SerializeToFileAsync(It.IsAny<WsbConfiguration>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task LaunchAsync_WhenSessionManagerUnsupported_FallsBackToFileLaunch()
    {
        // Arrange
        var mockSessions = new Mock<ISandboxSessionManager>();
        mockSessions.Setup(s => s.IsSupported).Returns(false);
        _mockValidator.Setup(v => v.Validate(It.IsAny<WsbConfiguration>())).Returns(new ValidationResult());

        var processStarterInvoked = false;
        var sut = new SandboxLauncher(
            _mockSerializer.Object,
            _mockValidator.Object,
            _mockRepository.Object,
            _tempDir,
            processStarter: _ => { processStarterInvoked = true; return null; },
            sandboxAvailability: () => true,
            sessionManager: mockSessions.Object);

        // Act
        var result = await sut.LaunchAsync(new WsbConfiguration());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.SessionId.Should().BeNull();
        result.WsbFilePath.Should().NotBeNull();
        processStarterInvoked.Should().BeTrue();
        mockSessions.Verify(
            s => s.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task LaunchAsync_WhenCliStartFails_ReturnsFailureWithCliError()
    {
        // Arrange - a broken CLI reports its own error rather than silently
        // falling back to a second launch path
        var mockSessions = new Mock<ISandboxSessionManager>();
        mockSessions.Setup(s => s.IsSupported).Returns(true);
        mockSessions
            .Setup(s => s.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WsbStartResult.Failure("wsb start failed: bad config"));
        _mockSerializer.Setup(s => s.Serialize(It.IsAny<WsbConfiguration>())).Returns("<Configuration />");
        _mockValidator.Setup(v => v.Validate(It.IsAny<WsbConfiguration>())).Returns(new ValidationResult());

        var sut = new SandboxLauncher(
            _mockSerializer.Object,
            _mockValidator.Object,
            _mockRepository.Object,
            _tempDir,
            sandboxAvailability: () => true,
            sessionManager: mockSessions.Object);

        // Act
        var result = await sut.LaunchAsync(new WsbConfiguration());

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("bad config");
    }

    [Fact]
    public async Task LaunchAsync_WhenCliNeverStarted_FallsBackToFileLaunch()
    {
        // Arrange - spawn failures and oversized configs are provably safe to
        // retry through the file path because the CLI never ran
        var mockSessions = new Mock<ISandboxSessionManager>();
        mockSessions.Setup(s => s.IsSupported).Returns(true);
        mockSessions
            .Setup(s => s.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WsbStartResult.Failure("too large", cliNeverStarted: true));
        _mockSerializer.Setup(s => s.Serialize(It.IsAny<WsbConfiguration>())).Returns("<Configuration />");
        _mockValidator.Setup(v => v.Validate(It.IsAny<WsbConfiguration>())).Returns(new ValidationResult());

        var processStarterInvoked = false;
        var sut = new SandboxLauncher(
            _mockSerializer.Object,
            _mockValidator.Object,
            _mockRepository.Object,
            _tempDir,
            processStarter: _ => { processStarterInvoked = true; return null; },
            sandboxAvailability: () => true,
            sessionManager: mockSessions.Object);

        // Act
        var result = await sut.LaunchAsync(new WsbConfiguration());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.WsbFilePath.Should().NotBeNull();
        processStarterInvoked.Should().BeTrue();
    }

    [Fact]
    public async Task LaunchAsync_WhenValidationFails_EmitsValidationKey()
    {
        // Arrange
        var invalidConfig = new WsbConfiguration();
        var failureResult = new ValidationResult();
        failureResult.AddError("MemoryInMB", "Invalid memory");
        _mockValidator.Setup(v => v.Validate(invalidConfig)).Returns(failureResult);

        // Act
        var result = await _sut.LaunchAsync(invalidConfig);

        // Assert
        result.ErrorKey.Should().Be(MessageKeys.LauncherValidationFailed);
        result.ErrorArgs.Should().BeEquivalentTo(["MemoryInMB", "Invalid memory"]);
    }

    [Fact]
    public async Task LaunchAsync_WhenSandboxUnavailable_EmitsNotInstalledKey()
    {
        // Arrange
        var sut = new SandboxLauncher(
            _mockSerializer.Object,
            _mockValidator.Object,
            _mockRepository.Object,
            _tempDir,
            sandboxAvailability: () => false);
        _mockValidator.Setup(v => v.Validate(It.IsAny<WsbConfiguration>())).Returns(new ValidationResult());

        // Act
        var result = await sut.LaunchAsync(new WsbConfiguration());

        // Assert
        result.ErrorKey.Should().Be(MessageKeys.LauncherNotInstalled);
    }

    [Fact]
    public async Task LaunchAsync_WhenCliRanAndFailed_PropagatesCliErrorKey()
    {
        // Arrange
        var mockSessions = new Mock<ISandboxSessionManager>();
        mockSessions.SetupGet(s => s.IsSupported).Returns(true);
        mockSessions
            .Setup(s => s.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WsbStartResult.Failure("boom", errorKey: MessageKeys.CliNoSandboxId));
        _mockValidator.Setup(v => v.Validate(It.IsAny<WsbConfiguration>())).Returns(new ValidationResult());
        _mockSerializer.Setup(s => s.Serialize(It.IsAny<WsbConfiguration>())).Returns("<Configuration/>");

        var sut = new SandboxLauncher(
            _mockSerializer.Object,
            _mockValidator.Object,
            _mockRepository.Object,
            _tempDir,
            sandboxAvailability: () => true,
            sessionManager: mockSessions.Object);

        // Act
        var result = await sut.LaunchAsync(new WsbConfiguration());

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorKey.Should().Be(MessageKeys.CliNoSandboxId, "the CLI failure key propagates to the launch result");
    }

    [Fact]
    public async Task LaunchAsync_WhenValidationErrorIsKeyed_PropagatesInnerKeyAndArgs()
    {
        // Arrange
        var invalidConfig = new WsbConfiguration();
        var failureResult = new ValidationResult();
        failureResult.AddError("MemoryInMB", "English fallback detail", MessageKeys.MemoryBelowMinimum, [128, 256]);
        _mockValidator.Setup(v => v.Validate(invalidConfig)).Returns(failureResult);

        // Act
        var result = await _sut.LaunchAsync(invalidConfig);

        // Assert — the structured inner key propagates so hosts can localize the
        // actual validation message rather than a wrapper around English text.
        result.ErrorKey.Should().Be(MessageKeys.MemoryBelowMinimum);
        result.ErrorArgs.Should().BeEquivalentTo([128, 256]);
    }
}
