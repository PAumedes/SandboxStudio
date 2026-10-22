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
        _sut = new JsonProfileRepository(_tempDir);
    }

    public void Dispose()
    {
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
    public void GetDefaultPresets_ReturnsPreconfiguredSecurityAndDevPresets()
    {
        // Act
        var presets = _sut.GetDefaultPresets();

        // Assert
        presets.Should().NotBeEmpty();
        presets.Should().Contain(p => p.Name.Contains("Malware"));
        presets.Should().Contain(p => p.Name.Contains("Developer"));
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
