using FluentAssertions;
using SandboxStudio.Core.Models;
using Xunit;

namespace SandboxStudio.Tests.Unit.Models;

public sealed class SandboxProfileCopyTests
{
    [Fact]
    public void Clone_WhenProfileIsNull_ThrowsArgumentNullException()
    {
        var act = () => SandboxProfileCopy.Clone(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Clone_PreservesProfileMetadataAndAllConfigurationScalars()
    {
        var profile = CreateProfile();

        var clone = SandboxProfileCopy.Clone(profile);

        clone.Id.Should().Be(profile.Id);
        clone.Name.Should().Be(profile.Name);
        clone.Description.Should().Be(profile.Description);
        clone.Tags.Should().Equal(profile.Tags);
        clone.IconGlyph.Should().Be(profile.IconGlyph);
        clone.IsFavorite.Should().Be(profile.IsFavorite);
        clone.CreatedAt.Should().Be(profile.CreatedAt);
        clone.LastLaunchedAt.Should().Be(profile.LastLaunchedAt);
        clone.Configuration.VGpu.Should().Be("Disable");
        clone.Configuration.Networking.Should().Be("Default");
        clone.Configuration.MemoryInMB.Should().Be(8192);
        clone.Configuration.AudioInput.Should().Be("Enable");
        clone.Configuration.VideoInput.Should().Be("Disable");
        clone.Configuration.ProtectedClient.Should().Be("Enable");
        clone.Configuration.ClipboardRedirection.Should().Be("Disable");
        clone.Configuration.PrinterRedirection.Should().Be("Enable");
    }

    [Fact]
    public void Clone_CreatesIndependentTagsConfigurationAndNestedObjects()
    {
        var profile = CreateProfile();

        var clone = SandboxProfileCopy.Clone(profile);

        clone.Tags.Should().NotBeSameAs(profile.Tags);
        clone.Configuration.Should().NotBeSameAs(profile.Configuration);
        clone.Configuration.MappedFolders.Should().NotBeSameAs(profile.Configuration.MappedFolders);
        clone.Configuration.MappedFolders[0].Should().NotBeSameAs(profile.Configuration.MappedFolders[0]);
        clone.Configuration.LogonCommand.Should().NotBeSameAs(profile.Configuration.LogonCommand);
        clone.Configuration.MappedFolders[0].HostFolder.Should().Be(@"C:\Projects");
        clone.Configuration.MappedFolders[0].SandboxFolder.Should().Be(@"C:\SandboxProjects");
        clone.Configuration.MappedFolders[0].ReadOnly.Should().Be("true");
        clone.Configuration.LogonCommand!.Command.Should().Be("start.exe");

        clone.Tags[0] = "Changed";
        clone.Tags.Add("New");
        clone.Configuration.Networking = "Disable";
        clone.Configuration.MappedFolders[0].HostFolder = @"C:\Changed";
        clone.Configuration.MappedFolders[0].SandboxFolder = @"C:\ChangedSandbox";
        clone.Configuration.MappedFolders[0].IsReadOnly = false;
        clone.Configuration.LogonCommand!.Command = "changed.exe";

        profile.Tags.Should().Equal("Security", "Development");
        profile.Configuration.Networking.Should().Be("Default");
        profile.Configuration.MappedFolders[0].HostFolder.Should().Be(@"C:\Projects");
        profile.Configuration.MappedFolders[0].SandboxFolder.Should().Be(@"C:\SandboxProjects");
        profile.Configuration.MappedFolders[0].ReadOnly.Should().Be("true");
        profile.Configuration.LogonCommand!.Command.Should().Be("start.exe");
    }

    [Fact]
    public void Clone_WhenFoldersAreEmptyAndLogonCommandIsNull_PreservesEmptyEdges()
    {
        var profile = new SandboxProfile
        {
            Configuration = new WsbConfiguration
            {
                MappedFolders = new(),
                LogonCommand = null
            }
        };

        var clone = SandboxProfileCopy.Clone(profile);

        clone.Configuration.MappedFolders.Should().BeEmpty();
        clone.Configuration.MappedFolders.Should().NotBeSameAs(profile.Configuration.MappedFolders);
        clone.Configuration.LogonCommand.Should().BeNull();

        clone.Configuration.MappedFolders.Add(new WsbMappedFolder { HostFolder = @"C:\Data" });

        profile.Configuration.MappedFolders.Should().BeEmpty();
    }

    [Fact]
    public void Clone_WhenEditedDraftIsDiscarded_LeavesSourceUnchanged()
    {
        var profile = CreateProfile();

        var draft = SandboxProfileCopy.Clone(profile);
        draft.Name = "Edited profile";
        draft.Description = "Edited description";
        draft.Tags.Clear();
        draft.Configuration.MemoryInMB = null;
        draft.Configuration.MappedFolders[0].HostFolder = @"C:\Discarded";
        draft.Configuration.LogonCommand!.Command = "discarded.exe";

        profile.Name.Should().Be("Work profile");
        profile.Description.Should().Be("A profile used to validate deep copying.");
        profile.Tags.Should().Equal("Security", "Development");
        profile.Configuration.MemoryInMB.Should().Be(8192);
        profile.Configuration.MappedFolders[0].HostFolder.Should().Be(@"C:\Projects");
        profile.Configuration.LogonCommand!.Command.Should().Be("start.exe");
    }

    private static SandboxProfile CreateProfile()
    {
        return new SandboxProfile
        {
            Id = Guid.Parse("8e2c13e8-27b1-4dca-9b05-3067e1a090d1"),
            Name = "Work profile",
            Description = "A profile used to validate deep copying.",
            Tags = new() { "Security", "Development" },
            IconGlyph = "\uE7B8",
            IsFavorite = true,
            CreatedAt = new DateTimeOffset(2024, 2, 3, 4, 5, 6, TimeSpan.Zero),
            LastLaunchedAt = new DateTimeOffset(2024, 3, 4, 5, 6, 7, TimeSpan.Zero),
            Configuration = new WsbConfiguration
            {
                VGpu = "Disable",
                Networking = "Default",
                MemoryInMB = 8192,
                AudioInput = "Enable",
                VideoInput = "Disable",
                ProtectedClient = "Enable",
                ClipboardRedirection = "Disable",
                PrinterRedirection = "Enable",
                MappedFolders = new()
                {
                    new WsbMappedFolder
                    {
                        HostFolder = @"C:\Projects",
                        SandboxFolder = @"C:\SandboxProjects",
                        ReadOnly = "true"
                    }
                },
                LogonCommand = new WsbLogonCommand { Command = "start.exe" }
            }
        };
    }
}
