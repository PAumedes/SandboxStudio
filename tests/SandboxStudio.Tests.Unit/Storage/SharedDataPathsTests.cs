using FluentAssertions;
using SandboxStudio.Core.Localization;
using SandboxStudio.Core.Models;
using SandboxStudio.Core.Storage;
using SandboxStudio.Core.Validation;
using Xunit;

namespace SandboxStudio.Tests.Unit.Storage;

public sealed class SharedDataPathsTests
{
    [Fact]
    public void Folders_LiveUnderTheUserProfileDotFolder()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".sandboxstudio");

        SharedDataPaths.Root.Should().Be(root);
        SharedDataPaths.Scripts.Should().Be(Path.Combine(root, "Scripts"));
        SharedDataPaths.Runs.Should().Be(Path.Combine(root, "Runs"));
    }

    [Fact]
    public void Folders_AreNotFlaggedAsUnmappableAppDataPaths()
    {
        // Arrange — Windows Sandbox reads these, so they must stay mappable
        var configuration = new WsbConfiguration();
        configuration.MappedFolders.Add(new WsbMappedFolder { HostFolder = SharedDataPaths.Scripts });
        configuration.MappedFolders.Add(new WsbMappedFolder { HostFolder = SharedDataPaths.Runs, SandboxFolder = @"C:\Runs" });

        // Act
        var result = new WsbValidator().Validate(configuration);

        // Assert
        result.Messages.Should().NotContain(m => m.MessageKey == MessageKeys.HostFolderUnderAppData);
    }
}
