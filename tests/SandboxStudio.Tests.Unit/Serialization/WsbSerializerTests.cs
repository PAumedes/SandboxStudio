using FluentAssertions;
using SandboxStudio.Core.Models;
using SandboxStudio.Core.Serialization;
using Xunit;

namespace SandboxStudio.Tests.Unit.Serialization;

public sealed class WsbSerializerTests
{
    private readonly WsbSerializer _sut = new();

    [Fact]
    public void Serialize_WithStandardConfiguration_ProducesValidWsbXmlWithoutNamespaces()
    {
        // Arrange
        var config = new WsbConfiguration
        {
            VGpu = "Enable",
            Networking = "Disable",
            MemoryInMB = 4096,
            ProtectedClient = "Enable",
            MappedFolders = new()
            {
                new WsbMappedFolder
                {
                    HostFolder = @"C:\Tools",
                    SandboxFolder = @"C:\Users\WDAGUtilityAccount\Desktop\Tools",
                    ReadOnly = "true"
                }
            },
            LogonCommand = new WsbLogonCommand
            {
                Command = "cmd.exe /c echo Ready"
            }
        };

        // Act
        var xml = _sut.Serialize(config);

        // Assert
        xml.Should().NotBeNullOrWhiteSpace();
        xml.Should().Contain("<Configuration>");
        xml.Should().Contain("<VGpu>Enable</VGpu>");
        xml.Should().Contain("<Networking>Disable</Networking>");
        xml.Should().Contain("<MemoryInMB>4096</MemoryInMB>");
        xml.Should().Contain("<ProtectedClient>Enable</ProtectedClient>");
        xml.Should().Contain(@"<HostFolder>C:\Tools</HostFolder>");
        xml.Should().Contain("<Command>cmd.exe /c echo Ready</Command>");
        xml.Should().NotContain("xmlns:xsi");
        xml.Should().NotContain("xmlns:xsd");
    }

    [Fact]
    public void Serialize_WhenMappedFoldersEmpty_OmitsMappedFoldersElement()
    {
        // Arrange
        var config = new WsbConfiguration
        {
            Networking = "Enable"
        };

        // Act
        var xml = _sut.Serialize(config);

        // Assert
        xml.Should().NotContain("<MappedFolders>");
    }

    [Fact]
    public void RoundtripSerialization_MaintainsCompleteDataFidelity()
    {
        // Arrange
        var original = new WsbConfiguration
        {
            VGpu = "Disable",
            Networking = "Enable",
            MemoryInMB = 2048,
            AudioInput = "Disable",
            VideoInput = "Disable",
            ProtectedClient = "Enable",
            ClipboardRedirection = "Disable",
            PrinterRedirection = "Disable",
            MappedFolders = new()
            {
                new WsbMappedFolder
                {
                    HostFolder = @"C:\Data",
                    IsReadOnly = true
                }
            },
            LogonCommand = new WsbLogonCommand
            {
                Command = "powershell.exe -NoProfile"
            }
        };

        // Act
        var xml = _sut.Serialize(original);
        var restored = _sut.Deserialize(xml);

        // Assert
        restored.VGpu.Should().Be(original.VGpu);
        restored.Networking.Should().Be(original.Networking);
        restored.MemoryInMB.Should().Be(original.MemoryInMB);
        restored.AudioInput.Should().Be(original.AudioInput);
        restored.VideoInput.Should().Be(original.VideoInput);
        restored.ProtectedClient.Should().Be(original.ProtectedClient);
        restored.ClipboardRedirection.Should().Be(original.ClipboardRedirection);
        restored.PrinterRedirection.Should().Be(original.PrinterRedirection);
        restored.MappedFolders.Should().HaveCount(1);
        restored.MappedFolders[0].HostFolder.Should().Be(@"C:\Data");
        restored.MappedFolders[0].IsReadOnly.Should().BeTrue();
        restored.LogonCommand?.Command.Should().Be("powershell.exe -NoProfile");
    }

    [Fact]
    public void Deserialize_WhenXmlIsEmptyOrWhitespace_ThrowsArgumentException()
    {
        // Act
        var act = () => _sut.Deserialize("   ");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Serialize_WhenMappedFolderHasEmptySandboxFolder_OmitsSandboxFolderElement()
    {
        // Arrange
        var config = new WsbConfiguration
        {
            MappedFolders = new()
            {
                new WsbMappedFolder
                {
                    HostFolder = @"C:\Tools",
                    SandboxFolder = ""
                }
            }
        };

        // Act
        var xml = _sut.Serialize(config);

        // Assert
        xml.Should().NotContain("<SandboxFolder>");
    }

    [Fact]
    public void Serialize_WhenLogonCommandHasEmptyCommand_OmitsLogonCommandElement()
    {
        // Arrange
        var config = new WsbConfiguration
        {
            LogonCommand = new WsbLogonCommand { Command = "   " }
        };

        // Act
        var xml = _sut.Serialize(config);

        // Assert
        xml.Should().NotContain("<LogonCommand>");
    }

    [Fact]
    public void Serialize_HandlesXmlEscapingForSpecialCharacters()
    {
        // Arrange
        var config = new WsbConfiguration
        {
            LogonCommand = new WsbLogonCommand
            {
                Command = "cmd.exe /c if 1 < 2 echo A & echo B > output.txt"
            }
        };

        // Act
        var xml = _sut.Serialize(config);
        var deserialized = _sut.Deserialize(xml);

        // Assert
        xml.Should().Contain("&lt;");
        xml.Should().Contain("&gt;");
        xml.Should().Contain("&amp;");
        deserialized.LogonCommand?.Command.Should().Be(config.LogonCommand.Command);
    }
}
