using FluentAssertions;
using SandboxStudio.Core.Models;
using SandboxStudio.Core.Scripts;
using Xunit;

namespace SandboxStudio.Tests.Unit.Scripts;

public sealed class LogonScriptServiceTests : IDisposable
{
    private static readonly Guid ProfileId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly string _tempDir;
    private readonly LogonScriptService _sut;

    public LogonScriptServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SandboxStudioScriptsTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _sut = new LogonScriptService(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private string ScriptFilePath => Path.Combine(_tempDir, $"{ProfileId:N}.cmd");

    [Fact]
    public async Task ApplyAsync_WritesScriptFileWithCommands()
    {
        // Arrange
        var configuration = new WsbConfiguration();

        // Act
        await _sut.ApplyAsync(configuration, ProfileId, ["echo one", "echo two"]);

        // Assert
        var content = await File.ReadAllTextAsync(ScriptFilePath);
        content.Should().Contain("@echo off");
        content.Should().Contain("echo one");
        content.Should().Contain("echo two");
    }

    [Fact]
    public async Task ApplyAsync_WiresReadOnlyMappingAndPackagedLogonCommand()
    {
        // Arrange
        var configuration = new WsbConfiguration();

        // Act
        await _sut.ApplyAsync(configuration, ProfileId, ["echo hi"]);

        // Assert
        var mapping = configuration.MappedFolders.Should().ContainSingle().Subject;
        mapping.HostFolder.Should().Be(_tempDir);
        mapping.SandboxFolder.Should().Be(LogonScriptService.SandboxScriptDirectory);
        mapping.IsReadOnly.Should().BeTrue();
        configuration.LogonCommand!.Command.Should().Be(
            $"cmd.exe /c \"{LogonScriptService.SandboxScriptDirectory}\\{ProfileId:N}.cmd\"");
    }

    [Fact]
    public async Task ApplyAsync_WhenMappingExists_DoesNotDuplicateIt()
    {
        // Arrange
        var configuration = new WsbConfiguration();
        await _sut.ApplyAsync(configuration, ProfileId, ["echo first"]);

        // Act — saving again with edited commands must not stack mappings
        await _sut.ApplyAsync(configuration, ProfileId, ["echo second"]);

        // Assert
        configuration.MappedFolders.Should().ContainSingle();
        (await File.ReadAllTextAsync(ScriptFilePath)).Should().Contain("echo second");
    }

    [Fact]
    public async Task ApplyAsync_WhenManagedMappingWasMadeWritable_RepairsReadOnly()
    {
        // Arrange — the editor shows the managed row; a user may uncheck Read-only
        var configuration = new WsbConfiguration();
        await _sut.ApplyAsync(configuration, ProfileId, ["echo first"]);
        configuration.MappedFolders[0].IsReadOnly = false;

        // Act
        await _sut.ApplyAsync(configuration, ProfileId, ["echo second"]);

        // Assert — the managed mount's read-only contract is restored
        configuration.MappedFolders.Should().ContainSingle();
        configuration.MappedFolders[0].IsReadOnly.Should().BeTrue();
        configuration.MappedFolders[0].HostFolder.Should().Be(_tempDir);
    }

    [Fact]
    public async Task ApplyAsync_PreservesUnrelatedUserMappings()
    {
        // Arrange
        var configuration = new WsbConfiguration();
        configuration.MappedFolders.Add(new WsbMappedFolder
        {
            HostFolder = "C:\\Users\\Public\\Tools",
            SandboxFolder = "C:\\Tools"
        });

        // Act
        await _sut.ApplyAsync(configuration, ProfileId, ["echo hi"]);

        // Assert
        configuration.MappedFolders.Should().HaveCount(2);
        configuration.MappedFolders.Should().Contain(f => f.HostFolder == "C:\\Users\\Public\\Tools");
    }

    [Fact]
    public async Task ApplyAsync_WhenCommandsAreEmpty_Throws()
    {
        // Arrange
        var configuration = new WsbConfiguration();

        // Act
        var act = () => _sut.ApplyAsync(configuration, ProfileId, []);

        // Assert — callers must not package an empty script
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task IsScriptBacked_DetectsManagedLogonCommand()
    {
        // Arrange
        var configuration = new WsbConfiguration();
        configuration.LogonCommand = new WsbLogonCommand { Command = "notepad.exe" };
        _sut.IsScriptBacked(configuration, ProfileId).Should().BeFalse();

        // Act
        await _sut.ApplyAsync(configuration, ProfileId, ["echo hi"]);

        // Assert
        _sut.IsScriptBacked(configuration, ProfileId).Should().BeTrue();
    }

    [Fact]
    public async Task UnapplyAsync_RemovesManagedArtifactsButKeepsUserContent()
    {
        // Arrange
        var configuration = new WsbConfiguration();
        configuration.MappedFolders.Add(new WsbMappedFolder
        {
            HostFolder = "C:\\Users\\Public\\Tools",
            SandboxFolder = "C:\\Tools"
        });
        await _sut.ApplyAsync(configuration, ProfileId, ["echo hi"]);

        // Act
        await _sut.UnapplyAsync(configuration, ProfileId);

        // Assert — config-level teardown only; file cleanup is DeleteAsync's job
        // so a cancelled save cannot leave a profile pointing at a deleted script.
        configuration.LogonCommand.Should().BeNull();
        configuration.MappedFolders.Should().ContainSingle(f => f.HostFolder == "C:\\Users\\Public\\Tools");
        File.Exists(ScriptFilePath).Should().BeTrue();
    }

    [Fact]
    public async Task UnapplyAsync_WhenUserEditedCommandAway_LeavesItAlone()
    {
        // Arrange — user replaced the packaged command with a custom one manually
        var configuration = new WsbConfiguration();
        await _sut.ApplyAsync(configuration, ProfileId, ["echo hi"]);
        configuration.LogonCommand = new WsbLogonCommand { Command = "custom.exe" };

        // Act
        await _sut.UnapplyAsync(configuration, ProfileId);

        // Assert — the custom command survives; the managed mount still goes
        configuration.LogonCommand!.Command.Should().Be("custom.exe");
        configuration.MappedFolders.Should().BeEmpty();
    }

    [Fact]
    public async Task UnapplyAsync_WhenCommandReferencesAnotherProfilesScript_KeepsMountAndCommand()
    {
        // Arrange — imported profile referencing a different profile's script
        var foreignId = Guid.NewGuid();
        var foreignCommand = $"cmd.exe /c \"{LogonScriptService.SandboxScriptDirectory}\\{foreignId:N}.cmd\"";
        var configuration = new WsbConfiguration();
        await _sut.ApplyAsync(configuration, ProfileId, ["echo hi"]);
        configuration.LogonCommand = new WsbLogonCommand { Command = foreignCommand };

        // Act
        await _sut.UnapplyAsync(configuration, ProfileId);

        // Assert — tearing the mount down would break the foreign command
        configuration.LogonCommand!.Command.Should().Be(foreignCommand);
        configuration.MappedFolders.Should().ContainSingle();
    }

    [Fact]
    public async Task RehomeAsync_WhenScriptFileExists_RetargetsCommandToNewId()
    {
        // Arrange — a profile imported from a .wsb referencing the source's script
        var foreignId = Guid.NewGuid();
        Directory.CreateDirectory(_tempDir);
        await File.WriteAllTextAsync(Path.Combine(_tempDir, $"{foreignId:N}.cmd"), "@echo off\necho x\r\n");
        var configuration = new WsbConfiguration
        {
            LogonCommand = new WsbLogonCommand
            {
                Command = $"cmd.exe /c \"{LogonScriptService.SandboxScriptDirectory}\\{foreignId:N}.cmd\""
            }
        };

        // Act
        await _sut.RehomeAsync(configuration, ProfileId);

        // Assert
        configuration.LogonCommand!.Command.Should().Be(
            $"cmd.exe /c \"{LogonScriptService.SandboxScriptDirectory}\\{ProfileId:N}.cmd\"");
        File.Exists(ScriptFilePath).Should().BeTrue();
        (await File.ReadAllTextAsync(ScriptFilePath)).Should().Contain("echo x");
        configuration.MappedFolders.Should().ContainSingle(f => f.SandboxFolder == LogonScriptService.SandboxScriptDirectory);
    }

    [Fact]
    public async Task RehomeAsync_WhenScriptFileIsMissing_LeavesCommandUntouched()
    {
        // Arrange — the referenced script no longer exists on this host
        var foreignId = Guid.NewGuid();
        var original = $"cmd.exe /c \"{LogonScriptService.SandboxScriptDirectory}\\{foreignId:N}.cmd\"";
        var configuration = new WsbConfiguration
        {
            LogonCommand = new WsbLogonCommand { Command = original }
        };

        // Act
        await _sut.RehomeAsync(configuration, ProfileId);

        // Assert — preserve import fidelity; nothing can be recovered
        configuration.LogonCommand!.Command.Should().Be(original);
    }

    [Fact]
    public async Task RehomeAsync_WhenCommandIsNotManaged_DoesNothing()
    {
        // Arrange
        var configuration = new WsbConfiguration
        {
            LogonCommand = new WsbLogonCommand { Command = "notepad.exe" }
        };

        // Act
        await _sut.RehomeAsync(configuration, ProfileId);

        // Assert
        configuration.LogonCommand!.Command.Should().Be("notepad.exe");
        configuration.MappedFolders.Should().BeEmpty();
    }

    [Fact]
    public async Task PruneOrphans_DeletesScriptsWithoutLiveProfiles()
    {
        // Arrange — one live profile plus a stale script from a deleted profile
        await _sut.ApplyAsync(new WsbConfiguration(), ProfileId, ["echo live"]);
        var orphanPath = Path.Combine(_tempDir, $"{Guid.NewGuid():N}.cmd");
        await File.WriteAllTextAsync(orphanPath, "@echo off");

        // Act
        _sut.PruneOrphans(new HashSet<Guid> { ProfileId });

        // Assert
        File.Exists(ScriptFilePath).Should().BeTrue();
        File.Exists(orphanPath).Should().BeFalse();
    }

    [Fact]
    public void HasNonAsciiContent_DetectsNonAsciiCommands()
    {
        _sut.HasNonAsciiContent(["echo hello", "dir C:\\"]).Should().BeFalse();
        _sut.HasNonAsciiContent(["echo héllo"]).Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_RemovesOnlyTheScriptFile()
    {
        // Arrange
        var configuration = new WsbConfiguration();
        await _sut.ApplyAsync(configuration, ProfileId, ["echo hi"]);

        // Act
        await _sut.DeleteAsync(ProfileId);

        // Assert
        File.Exists(ScriptFilePath).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_WhenNoScriptExists_DoesNotThrow()
    {
        // Act & Assert
        await _sut.DeleteAsync(ProfileId);
    }
}
