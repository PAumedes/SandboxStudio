using FluentAssertions;
using Moq;
using SandboxStudio.Core.Models;
using SandboxStudio.Core.Serialization;
using SandboxStudio.Core.Validation;
using Xunit;

namespace SandboxStudio.Tests.Unit.Serialization;

public sealed class WsbExportServiceTests : IDisposable
{
    private readonly string _tempDirectory;

    public WsbExportServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "SandboxStudioExportTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ExportAsync_WhenConfigurationIsInvalid_DoesNotCallSerializerOrChangeTarget()
    {
        var configuration = new WsbConfiguration { Networking = "Unsupported" };
        var targetPath = Path.Combine(_tempDirectory, "Existing.wsb");
        const string existingContent = "Existing target contents";
        await File.WriteAllTextAsync(targetPath, existingContent);
        var serializer = new Mock<IWsbSerializer>();
        var service = new WsbExportService(new WsbValidator(), serializer.Object);

        var result = await service.ExportAsync(configuration, targetPath);

        result.IsValid.Should().BeFalse();
        result.Messages.Should().Contain(message =>
            message.Severity == ValidationSeverity.Error &&
            message.PropertyName == nameof(WsbConfiguration.Networking));
        (await File.ReadAllTextAsync(targetPath)).Should().Be(existingContent);
        serializer.Verify(
            instance => instance.SerializeToFileAsync(configuration, targetPath, It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExportAsync_WithValidConfiguration_PassesConfigurationPathAndCancellationToken()
    {
        var configuration = new WsbConfiguration { Networking = "Enable", MemoryInMB = 4096 };
        var targetPath = Path.Combine(_tempDirectory, "Valid.wsb");
        var cancellationToken = new CancellationTokenSource().Token;
        var validationResult = new ValidationResult();
        var validator = new Mock<IWsbValidator>();
        validator.Setup(instance => instance.Validate(configuration)).Returns(validationResult);
        var serializer = new Mock<IWsbSerializer>();
        serializer
            .Setup(instance => instance.SerializeToFileAsync(configuration, targetPath, cancellationToken))
            .Returns(Task.CompletedTask);
        var service = new WsbExportService(validator.Object, serializer.Object);

        var result = await service.ExportAsync(configuration, targetPath, cancellationToken);

        result.Should().BeSameAs(validationResult);
        validator.Verify(instance => instance.Validate(configuration), Times.Once);
        serializer.Verify(
            instance => instance.SerializeToFileAsync(configuration, targetPath, cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task ExportAsync_WhenValidationReturnsWarnings_ExportsAndReturnsWarnings()
    {
        var configuration = new WsbConfiguration { MemoryInMB = 512 };
        var targetPath = Path.Combine(_tempDirectory, "Warning.wsb");
        var serializer = new Mock<IWsbSerializer>();
        serializer
            .Setup(instance => instance.SerializeToFileAsync(
                configuration,
                targetPath,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var service = new WsbExportService(new WsbValidator(), serializer.Object);

        var result = await service.ExportAsync(configuration, targetPath);

        result.IsValid.Should().BeTrue();
        result.Messages.Should().ContainSingle(message =>
            message.Severity == ValidationSeverity.Warning &&
            message.PropertyName == nameof(WsbConfiguration.MemoryInMB));
        serializer.Verify(
            instance => instance.SerializeToFileAsync(
                configuration,
                targetPath,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExportAsync_WhenConfigurationIsNull_ThrowsBeforeValidationOrSerialization()
    {
        var validator = new Mock<IWsbValidator>();
        var serializer = new Mock<IWsbSerializer>();
        var service = new WsbExportService(validator.Object, serializer.Object);

        Func<Task> act = async () =>
        {
            await service.ExportAsync(null!, Path.Combine(_tempDirectory, "Null.wsb"));
        };

        await act.Should().ThrowAsync<ArgumentNullException>();
        validator.Verify(instance => instance.Validate(It.IsAny<WsbConfiguration>()), Times.Never);
        serializer.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExportAsync_WhenFilePathIsNull_ThrowsBeforeValidationOrSerialization()
    {
        var validator = new Mock<IWsbValidator>();
        var serializer = new Mock<IWsbSerializer>();
        var service = new WsbExportService(validator.Object, serializer.Object);

        Func<Task> act = async () =>
        {
            await service.ExportAsync(new WsbConfiguration(), null!);
        };

        await act.Should().ThrowAsync<ArgumentNullException>();
        validator.Verify(instance => instance.Validate(It.IsAny<WsbConfiguration>()), Times.Never);
        serializer.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public async Task ExportAsync_WhenFilePathIsWhitespace_ThrowsBeforeValidationOrSerialization(string filePath)
    {
        var validator = new Mock<IWsbValidator>();
        var serializer = new Mock<IWsbSerializer>();
        var service = new WsbExportService(validator.Object, serializer.Object);

        Func<Task> act = async () =>
        {
            await service.ExportAsync(new WsbConfiguration(), filePath);
        };

        await act.Should().ThrowAsync<ArgumentException>();
        validator.Verify(instance => instance.Validate(It.IsAny<WsbConfiguration>()), Times.Never);
        serializer.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExportAsync_WhenSerializerThrowsIoException_PropagatesIt()
    {
        var configuration = new WsbConfiguration { Networking = "Disable" };
        var targetPath = Path.Combine(_tempDirectory, "Failure.wsb");
        var serializer = new Mock<IWsbSerializer>();
        serializer
            .Setup(instance => instance.SerializeToFileAsync(
                configuration,
                targetPath,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("The target is not writable."));
        var service = new WsbExportService(new WsbValidator(), serializer.Object);

        Func<Task> act = async () =>
        {
            await service.ExportAsync(configuration, targetPath);
        };

        await act.Should().ThrowAsync<IOException>()
            .WithMessage("The target is not writable.");
    }

    [Fact]
    public async Task ExportAsync_WithRealSerializer_WritesAndRoundtripsConfiguration()
    {
        var configuration = new WsbConfiguration
        {
            Networking = "Disable",
            MemoryInMB = 3072
        };
        var serializer = new WsbSerializer();
        var service = new WsbExportService(new WsbValidator(), serializer);
        var targetPath = Path.Combine(_tempDirectory, "Roundtrip.wsb");

        var result = await service.ExportAsync(configuration, targetPath);

        result.IsValid.Should().BeTrue();
        File.Exists(targetPath).Should().BeTrue();
        var restored = await serializer.DeserializeFromFileAsync(targetPath);
        restored.Networking.Should().Be("Disable");
        restored.MemoryInMB.Should().Be(3072);
    }
}
