using FluentAssertions;
using SandboxStudio.Core.Models;
using SandboxStudio.Core.Validation;
using Xunit;

namespace SandboxStudio.Tests.Unit.Validation;

public sealed class WsbValidatorTests
{
    private readonly WsbValidator _sut = new();

    [Fact]
    public void Validate_ValidConfiguration_ReturnsSuccess()
    {
        // Arrange
        var config = new WsbConfiguration
        {
            VGpu = "Enable",
            Networking = "Default",
            MemoryInMB = 4096,
            MappedFolders = new()
            {
                new WsbMappedFolder
                {
                    HostFolder = Environment.SystemDirectory,
                    ReadOnly = "true"
                }
            }
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Messages.Should().BeEmpty();
    }

    [Theory]
    [InlineData(128)]
    [InlineData(0)]
    [InlineData(-512)]
    public void Validate_WhenMemoryBelow256MB_ReturnsError(int memoryMb)
    {
        // Arrange
        var config = new WsbConfiguration
        {
            MemoryInMB = memoryMb
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Messages.Should().Contain(m => m.Severity == ValidationSeverity.Error && m.PropertyName == nameof(WsbConfiguration.MemoryInMB));
    }

    [Fact]
    public void Validate_WhenMemoryBetween256And1024MB_ReturnsWarning()
    {
        // Arrange
        var config = new WsbConfiguration
        {
            MemoryInMB = 512
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.Should().BeTrue(); // Warnings do not invalidate configuration
        result.Messages.Should().Contain(m => m.Severity == ValidationSeverity.Warning && m.PropertyName == nameof(WsbConfiguration.MemoryInMB));
    }

    [Fact]
    public void Validate_WhenTriStateHasInvalidValue_ReturnsError()
    {
        // Arrange
        var config = new WsbConfiguration
        {
            Networking = "SuperFast" // Invalid
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Messages.Should().Contain(m => m.Severity == ValidationSeverity.Error && m.PropertyName == nameof(WsbConfiguration.Networking));
    }

    [Fact]
    public void Validate_WhenHostFolderIsEmpty_ReturnsError()
    {
        // Arrange
        var config = new WsbConfiguration
        {
            MappedFolders = new()
            {
                new WsbMappedFolder { HostFolder = "   " }
            }
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Messages.Should().Contain(m => m.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void Validate_WhenCommandExceedsLimit_ReturnsError()
    {
        // Arrange
        var config = new WsbConfiguration
        {
            LogonCommand = new WsbLogonCommand
            {
                Command = new string('A', 8200) // Exceeds 8191
            }
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Messages.Should().Contain(m => m.Severity == ValidationSeverity.Error && m.PropertyName == nameof(WsbConfiguration.LogonCommand));
    }

    [Fact]
    public void Validate_WhenHostFolderIsNotRooted_ReturnsError()
    {
        // Arrange
        var config = new WsbConfiguration
        {
            MappedFolders = new()
            {
                new WsbMappedFolder { HostFolder = @"relative\path" }
            }
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Messages.Should().Contain(m => m.Severity == ValidationSeverity.Error && m.Message.Contains("must be an absolute rooted path"));
    }

    [Fact]
    public void Validate_WhenDuplicateHostFolder_ReturnsError()
    {
        // Arrange
        var config = new WsbConfiguration
        {
            MappedFolders = new()
            {
                new WsbMappedFolder { HostFolder = @"C:\Folder" },
                new WsbMappedFolder { HostFolder = @"c:\folder" }
            }
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Messages.Should().Contain(m => m.Severity == ValidationSeverity.Error && m.Message.Contains("Duplicate HostFolder"));
    }

    [Fact]
    public void Validate_WhenDuplicateSandboxFolder_ReturnsError()
    {
        // Arrange
        var config = new WsbConfiguration
        {
            MappedFolders = new()
            {
                new WsbMappedFolder { HostFolder = @"C:\Folder1", SandboxFolder = @"C:\Desktop\Target" },
                new WsbMappedFolder { HostFolder = @"C:\Folder2", SandboxFolder = @"c:\desktop\target" }
            }
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Messages.Should().Contain(m => m.Severity == ValidationSeverity.Error && m.Message.Contains("Duplicate SandboxFolder"));
    }

    [Fact]
    public void Validate_WhenTriStateIsWhitespace_ReturnsError()
    {
        // Arrange
        var config = new WsbConfiguration
        {
            VGpu = "   "
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Messages.Should().Contain(m => m.Severity == ValidationSeverity.Error && m.Message.Contains("cannot be empty or whitespace"));
    }
}
