using FluentAssertions;
using SandboxStudio.Core.Localization;
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

    [Theory]
    [InlineData("enable")]
    [InlineData("DISABLE")]
    [InlineData("default")]
    [InlineData("Enabled")]
    public void Validate_WhenTriStateHasNonCanonicalCasing_ReturnsError(string value)
    {
        // Arrange - Windows Sandbox only accepts canonical "Default", "Enable", "Disable" casing
        var config = new WsbConfiguration
        {
            Networking = value
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
    public void Validate_WhenCommandIsAtMaximumLength_ReturnsValid()
    {
        var config = new WsbConfiguration
        {
            LogonCommand = new WsbLogonCommand
            {
                Command = new string('A', 8191)
            }
        };

        var result = _sut.Validate(config);

        result.IsValid.Should().BeTrue();
        result.Messages.Should().NotContain(m =>
            m.Severity == ValidationSeverity.Error &&
            m.PropertyName == nameof(WsbConfiguration.LogonCommand));
    }

    [Fact]
    public void Validate_WhenCommandIsOneCharacterOverMaximumLength_ReturnsError()
    {
        var config = new WsbConfiguration
        {
            LogonCommand = new WsbLogonCommand
            {
                Command = new string('A', 8192)
            }
        };

        var result = _sut.Validate(config);

        result.IsValid.Should().BeFalse();
        result.Messages.Should().Contain(m => m.Severity == ValidationSeverity.Error && m.PropertyName == nameof(WsbConfiguration.LogonCommand));
    }

    [Fact]
    public void Validate_WhenLogonCommandIsWhitespace_DoesNotInvalidateConfiguration()
    {
        var config = new WsbConfiguration
        {
            LogonCommand = new WsbLogonCommand { Command = " \t " }
        };

        var result = _sut.Validate(config);

        result.IsValid.Should().BeTrue();
        result.Messages.Should().NotContain(m =>
            m.Severity == ValidationSeverity.Error &&
            m.PropertyName == nameof(WsbConfiguration.LogonCommand));
    }

    [Fact]
    public void Validate_WhenHostFolderIsNotFullyQualified_ReturnsError()
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
        result.Messages.Should().Contain(m => m.Severity == ValidationSeverity.Error && m.Message.Contains("must be a fully qualified absolute path"));
    }

    [Theory]
    [InlineData(@"\AppData\SandboxStudio")]
    [InlineData(@"C:SandboxStudio")]
    public void Validate_WhenHostFolderIsRootedButNotFullyQualified_ReturnsError(string hostFolder)
    {
        var config = new WsbConfiguration
        {
            MappedFolders = new()
            {
                new WsbMappedFolder { HostFolder = hostFolder }
            }
        };

        var result = _sut.Validate(config);

        result.IsValid.Should().BeFalse();
        result.Messages.Should().Contain(m =>
            m.Severity == ValidationSeverity.Error &&
            m.PropertyName == "MappedFolders[0].HostFolder" &&
            m.Message.Contains("must be a fully qualified absolute path"));
    }

    [Theory]
    [InlineData(@"\AppData\SandboxStudio")]
    [InlineData(@"C:SandboxStudio")]
    public void Validate_WhenSandboxFolderIsRootedButNotFullyQualified_ReturnsError(string sandboxFolder)
    {
        var config = new WsbConfiguration
        {
            MappedFolders = new()
            {
                new WsbMappedFolder
                {
                    HostFolder = Environment.SystemDirectory,
                    SandboxFolder = sandboxFolder
                }
            }
        };

        var result = _sut.Validate(config);

        result.IsValid.Should().BeFalse();
        result.Messages.Should().Contain(m =>
            m.Severity == ValidationSeverity.Error &&
            m.PropertyName == "MappedFolders[0].SandboxFolder" &&
            m.Message.Contains("must be a fully qualified absolute path"));
    }

    [Theory]
    [InlineData(@"C:\Sandbox\Destination")]
    [InlineData(@"\\server\share\Sandbox")]
    public void Validate_WhenSandboxFolderIsFullyQualified_AcceptsDriveAndUncPaths(string sandboxFolder)
    {
        var config = new WsbConfiguration
        {
            MappedFolders = new()
            {
                new WsbMappedFolder
                {
                    HostFolder = Environment.SystemDirectory,
                    SandboxFolder = sandboxFolder
                }
            }
        };

        var result = _sut.Validate(config);

        result.IsValid.Should().BeTrue();
        result.Messages.Should().NotContain(m => m.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void Validate_WhenHostFolderDoesNotExist_ReturnsWarningWithoutError()
    {
        var missingHostFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "Missing");
        var config = new WsbConfiguration
        {
            MappedFolders = new()
            {
                new WsbMappedFolder { HostFolder = missingHostFolder }
            }
        };

        var result = _sut.Validate(config);

        result.IsValid.Should().BeTrue();
        result.Messages.Should().ContainSingle(m =>
            m.Severity == ValidationSeverity.Warning &&
            m.PropertyName == "MappedFolders[0].HostFolder");
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

    [Fact]
    public void Validate_WhenMemoryBelowMinimum_EmitsMessageKeyAndArgs()
    {
        // Arrange
        var config = new WsbConfiguration { MemoryInMB = 128 };

        // Act
        var result = _sut.Validate(config);

        // Assert
        var message = result.Messages.Should().ContainSingle(m => m.PropertyName == nameof(WsbConfiguration.MemoryInMB)).Subject;
        message.MessageKey.Should().Be(MessageKeys.MemoryBelowMinimum);
        message.MessageArgs.Should().BeEquivalentTo([128, 256]);
        message.Message.Should().Contain("128").And.Contain("256", "the English fallback remains populated");
    }

    [Fact]
    public void Validate_WhenHostFolderMissing_EmitsWarningKeyAndPathArg()
    {
        // Arrange
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var config = new WsbConfiguration
        {
            MappedFolders =
            [
                new WsbMappedFolder { HostFolder = missing, SandboxFolder = "C:\\Share" }
            ]
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        var message = result.Messages.Should().ContainSingle(m => m.PropertyName.EndsWith(".HostFolder")).Subject;
        message.Severity.Should().Be(ValidationSeverity.Warning);
        message.MessageKey.Should().Be(MessageKeys.HostFolderMissing);
        message.MessageArgs.Should().BeEquivalentTo([missing]);
    }
}
