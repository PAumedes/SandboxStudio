using FluentAssertions;
using Moq;
using SandboxStudio.Core.Execution;
using SandboxStudio.Core.Models;
using SandboxStudio.Core.Serialization;
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
}
