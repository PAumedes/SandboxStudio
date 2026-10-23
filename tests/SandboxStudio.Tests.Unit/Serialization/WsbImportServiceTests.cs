using FluentAssertions;
using Moq;
using SandboxStudio.Core.Models;
using SandboxStudio.Core.Serialization;
using SandboxStudio.Core.Storage;
using SandboxStudio.Core.Validation;
using Xunit;

namespace SandboxStudio.Tests.Unit.Serialization;

public sealed class WsbImportServiceTests
{
    private readonly Mock<IWsbSerializer> _mockSerializer = new();
    private readonly Mock<IWsbValidator> _mockValidator = new();
    private readonly Mock<IProfileRepository> _mockRepository = new();
    private readonly WsbImportService _sut;

    public WsbImportServiceTests()
    {
        _sut = new WsbImportService(
            _mockSerializer.Object,
            _mockValidator.Object,
            _mockRepository.Object);
    }

    [Fact]
    public async Task ImportAsync_WhenFileIsValid_SavesProfileNamedAfterFile()
    {
        // Arrange
        var configuration = new WsbConfiguration { Networking = "Disable" };
        _mockSerializer
            .Setup(s => s.DeserializeFromFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(configuration);
        _mockValidator
            .Setup(v => v.Validate(configuration))
            .Returns(new ValidationResult());

        // Act
        var result = await _sut.ImportAsync(@"C:\Configs\Air Gapped Lab.wsb");

        // Assert
        result.Profile.Name.Should().Be("Air Gapped Lab");
        result.Profile.Description.Should().Contain("Air Gapped Lab.wsb");
        result.Profile.Configuration.Should().BeSameAs(configuration);
        result.Profile.Id.Should().NotBeEmpty();
        result.Validation.IsValid.Should().BeTrue();
        _mockRepository.Verify(r => r.SaveAsync(result.Profile, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ImportAsync_WhenValidationReportsErrors_StillSavesProfile()
    {
        // Arrange - imported profiles are kept even when invalid; the editor surfaces the errors
        var configuration = new WsbConfiguration { MemoryInMB = -1 };
        var validation = new ValidationResult();
        validation.AddError(nameof(WsbConfiguration.MemoryInMB), "Memory must be a positive number.");

        _mockSerializer
            .Setup(s => s.DeserializeFromFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(configuration);
        _mockValidator
            .Setup(v => v.Validate(configuration))
            .Returns(validation);

        // Act
        var result = await _sut.ImportAsync(@"C:\Configs\bad.wsb");

        // Assert
        result.Validation.IsValid.Should().BeFalse();
        _mockRepository.Verify(r => r.SaveAsync(result.Profile, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ImportAsync_WhenFileIsMalformed_PropagatesAndDoesNotSave()
    {
        // Arrange
        _mockSerializer
            .Setup(s => s.DeserializeFromFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Failed to deserialize"));

        // Act
        var act = () => _sut.ImportAsync(@"C:\Configs\broken.wsb");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        _mockRepository.Verify(r => r.SaveAsync(It.IsAny<SandboxProfile>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ImportAsync_WhenPathIsMissing_Throws(string? filePath)
    {
        // Act
        var act = () => _sut.ImportAsync(filePath!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
