using FluentAssertions;
using SandboxStudio.Core.Models;
using SandboxStudio.Core.Storage;
using Xunit;

namespace SandboxStudio.Tests.Unit.Storage;

public sealed class JsonProfileRepositoryTests : IDisposable
{
    private readonly string _tempDir;
    private readonly JsonProfileRepository _sut;

    public JsonProfileRepositoryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SandboxStudioTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _sut = new JsonProfileRepository(_tempDir);
    }

    public void Dispose()
    {
        _sut.Dispose();
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_And_GetByIdAsync_PersistsProfileAccurately()
    {
        // Arrange
        var profile = new SandboxProfile
        {
            Name = "Unit Test Profile",
            Description = "Testing storage",
            IsFavorite = true,
            Configuration = new WsbConfiguration
            {
                Networking = "Disable"
            }
        };

        // Act
        await _sut.SaveAsync(profile);
        var retrieved = await _sut.GetByIdAsync(profile.Id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Unit Test Profile");
        retrieved.Description.Should().Be("Testing storage");
        retrieved.IsFavorite.Should().BeTrue();
        retrieved.Configuration.Networking.Should().Be("Disable");
    }

    [Fact]
    public async Task DeleteAsync_RemovesProfileFromDisk()
    {
        // Arrange
        var profile = new SandboxProfile { Name = "To be deleted" };
        await _sut.SaveAsync(profile);

        // Act
        var deleted = await _sut.DeleteAsync(profile.Id);
        var retrieved = await _sut.GetByIdAsync(profile.Id);

        // Assert
        deleted.Should().BeTrue();
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_WhenStoredFileHasNullMembers_NormalizesToEmpty()
    {
        // Arrange - corrupt-but-loadable JSON can null out required members
        var id = Guid.NewGuid();
        await File.WriteAllTextAsync(
            Path.Combine(_tempDir, $"{id:N}.json"),
            $$"""{"Id":"{{id}}","Name":"Corrupt","Tags":null,"Configuration":null}""");

        // Act
        var profiles = await _sut.GetAllAsync();

        // Assert
        var profile = profiles.Should().ContainSingle().Subject;
        profile.Tags.Should().NotBeNull().And.BeEmpty();
        profile.Configuration.Should().NotBeNull();
        profile.Configuration.MappedFolders.Should().NotBeNull();
    }

    [Fact]
    public async Task GetByIdAsync_WhenStoredFileHasNullMembers_NormalizesToEmpty()
    {
        // Arrange
        var id = Guid.NewGuid();
        await File.WriteAllTextAsync(
            Path.Combine(_tempDir, $"{id:N}.json"),
            $$"""{"Id":"{{id}}","Name":"Corrupt","Tags":null,"Configuration":null}""");

        // Act
        var profile = await _sut.GetByIdAsync(id);

        // Assert
        profile.Should().NotBeNull();
        profile!.Tags.Should().NotBeNull().And.BeEmpty();
        profile.Configuration.Should().NotBeNull();
        profile.Configuration.MappedFolders.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsFavoritesFirstThenOrderedByName()
    {
        // Arrange
        var p1 = new SandboxProfile { Name = "Zebra", IsFavorite = false };
        var p2 = new SandboxProfile { Name = "Alpha", IsFavorite = false };
        var p3 = new SandboxProfile { Name = "Middle", IsFavorite = true };

        await _sut.SaveAsync(p1);
        await _sut.SaveAsync(p2);
        await _sut.SaveAsync(p3);

        // Act
        var all = await _sut.GetAllAsync();

        // Assert
        all.Should().HaveCount(3);
        all[0].Name.Should().Be("Middle"); // Favorite first
        all[1].Name.Should().Be("Alpha");  // Alphabetical next
        all[2].Name.Should().Be("Zebra");
    }

    [Fact]
    public async Task GetAllAsync_IgnoresCorruptJsonFiles()
    {
        // Arrange
        var valid = new SandboxProfile { Name = "Valid Profile" };
        await _sut.SaveAsync(valid);

        var corruptFile = Path.Combine(_tempDir, $"{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(corruptFile, "{ invalid json content !!!");

        // Act
        var all = await _sut.GetAllAsync();

        // Assert
        all.Should().ContainSingle();
        all[0].Name.Should().Be("Valid Profile");
    }

    [Fact]
    public async Task GetAllAsync_WhenDirectoryDoesNotExist_ReturnsEmpty()
    {
        using var sut = new JsonProfileRepository(Path.Combine(_tempDir, "NotCreatedYet"));

        (await sut.GetAllAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAsync_WhenDirectoryDoesNotExist_CreatesIt()
    {
        var dir = Path.Combine(_tempDir, "NotCreatedYet");
        using var sut = new JsonProfileRepository(dir);

        await sut.SaveAsync(new SandboxProfile { Name = "First" });

        (await sut.GetAllAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task GetAllAsync_WhenDirectoryIsDeletedMidSession_RecreatesIt()
    {
        var dir = Path.Combine(_tempDir, "Transient");
        using var sut = new JsonProfileRepository(dir);
        await sut.SaveAsync(new SandboxProfile { Name = "Gone" });
        Directory.Delete(dir, recursive: true);

        (await sut.GetAllAsync()).Should().BeEmpty();
        await sut.SaveAsync(new SandboxProfile { Name = "Back" });
        (await sut.GetAllAsync()).Should().ContainSingle(p => p.Name == "Back");
    }

    [Fact]
    public async Task GetAllAsync_IgnoresLegacyPresetMarkerFiles()
    {
        // Preset-seeding markers from earlier releases must not surface as profiles
        await File.WriteAllTextAsync(Path.Combine(_tempDir, ".initialized"), "3");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, ".initializing"), string.Empty);

        (await _sut.GetAllAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_DeletesLegacyPresetMarkerFiles()
    {
        // The removed preset-seeding machinery left these behind; they are cleaned up
        var initialized = Path.Combine(_tempDir, ".initialized");
        var initializing = Path.Combine(_tempDir, ".initializing");
        await File.WriteAllTextAsync(initialized, "3");
        await File.WriteAllTextAsync(initializing, string.Empty);

        await _sut.GetAllAsync();

        File.Exists(initialized).Should().BeFalse();
        File.Exists(initializing).Should().BeFalse();
    }

    [Fact]
    public async Task GetByIdAsync_WhenFileIsCorrupt_ReturnsNullInsteadOfCrashing()
    {
        // Arrange
        var corruptId = Guid.NewGuid();
        var corruptFile = Path.Combine(_tempDir, $"{corruptId:N}.json");
        await File.WriteAllTextAsync(corruptFile, "{ invalid json content !!!");

        // Act
        var result = await _sut.GetByIdAsync(corruptId);

        // Assert
        result.Should().BeNull();
    }
}
