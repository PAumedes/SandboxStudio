using FluentAssertions;
using SandboxStudio.Core.Models;
using SandboxStudio.Core.Storage;
using Xunit;

namespace SandboxStudio.Tests.Unit.Storage;

public sealed class JsonProfileRepositoryTests : IDisposable
{
    private static readonly Guid[] V1PresetIds =
    [
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Guid.Parse("22222222-2222-2222-2222-222222222222"),
        Guid.Parse("33333333-3333-3333-3333-333333333333")
    ];

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
    public async Task EnsureDefaultPresetsAsync_WhenDirectoryIsEmpty_SeedsBuiltInPresetsOnce()
    {
        var presets = _sut.GetDefaultPresets();

        await _sut.EnsureDefaultPresetsAsync();
        var firstLoad = await _sut.GetAllAsync();
        await _sut.EnsureDefaultPresetsAsync();
        var secondLoad = await _sut.GetAllAsync();

        firstLoad.Should().HaveCount(presets.Count);
        firstLoad.Select(profile => profile.Id).Should().BeEquivalentTo(presets.Select(preset => preset.Id));
        secondLoad.Select(profile => profile.Id).Should().BeEquivalentTo(presets.Select(preset => preset.Id));
        Directory.GetFiles(_tempDir, "*.json").Should().HaveCount(presets.Count);
        File.Exists(Path.Combine(_tempDir, ".initialized")).Should().BeTrue();
    }

    [Fact]
    public async Task EnsureDefaultPresetsAsync_WhenPresetWriteFailsAfterPartialSeed_RetryCompletesMissingPresets()
    {
        var presets = _sut.GetDefaultPresets();
        var blockedTargetPath = Path.Combine(_tempDir, $"{presets[1].Id:N}.json");
        Directory.CreateDirectory(blockedTargetPath);

        var initialize = () => _sut.EnsureDefaultPresetsAsync();
        await initialize.Should().ThrowAsync<UnauthorizedAccessException>();
        var profiles = await _sut.GetAllAsync();

        profiles.Select(profile => profile.Id).Should().BeEquivalentTo([presets[0].Id]);
        File.Exists(Path.Combine(_tempDir, ".initialized")).Should().BeFalse();
        File.Exists(Path.Combine(_tempDir, ".initializing")).Should().BeTrue();

        Directory.Delete(blockedTargetPath);
        await _sut.EnsureDefaultPresetsAsync();
        profiles = await _sut.GetAllAsync();

        profiles.Should().HaveCount(presets.Count);
        profiles.Select(profile => profile.Id).Should().BeEquivalentTo(presets.Select(preset => preset.Id));
        File.Exists(Path.Combine(_tempDir, ".initialized")).Should().BeTrue();
        File.Exists(Path.Combine(_tempDir, ".initializing")).Should().BeFalse();
    }

    [Fact]
    public async Task EnsureDefaultPresetsAsync_WhenAllProfilesAreDeletedAndRepositoryReopens_DoesNotReseed()
    {
        await _sut.EnsureDefaultPresetsAsync();
        var profiles = await _sut.GetAllAsync();
        profiles.Select(profile => profile.Id).Should().BeEquivalentTo(
            _sut.GetDefaultPresets().Select(preset => preset.Id));

        foreach (var profile in profiles)
        {
            (await _sut.DeleteAsync(profile.Id)).Should().BeTrue();
        }

        _sut.Dispose();
        using var reopenedRepository = new JsonProfileRepository(_tempDir);
        await reopenedRepository.EnsureDefaultPresetsAsync();

        (await reopenedRepository.GetAllAsync()).Should().BeEmpty();
        File.Exists(Path.Combine(_tempDir, ".initialized")).Should().BeTrue();
    }

    [Fact]
    public async Task EnsureDefaultPresetsAsync_WhenStoreWasSeededAtVersion1_SeedsOnlyNewlyIntroducedPresets()
    {
        // Arrange - a store seeded before the new presets existed carries version "1"
        await File.WriteAllTextAsync(Path.Combine(_tempDir, ".initialized"), "1");
        var newPresetIds = _sut.GetDefaultPresets()
            .Select(p => p.Id)
            .Except(V1PresetIds)
            .ToList();
        newPresetIds.Should().NotBeEmpty("the seed catalog must grow for this test to mean anything");

        // Act
        await _sut.EnsureDefaultPresetsAsync();
        var profiles = await _sut.GetAllAsync();

        // Assert - only presets introduced after version 1 appear; deleted v1 presets stay deleted
        profiles.Select(p => p.Id).Should().BeEquivalentTo(newPresetIds);
        File.ReadAllText(Path.Combine(_tempDir, ".initialized")).Should().Be("2");
    }

    [Fact]
    public async Task EnsureDefaultPresetsAsync_WhenMarkerIsEmpty_TreatsStoreAsVersion1()
    {
        // Arrange - pre-versioning stores wrote an empty marker file
        await File.WriteAllTextAsync(Path.Combine(_tempDir, ".initialized"), string.Empty);
        var newPresetIds = _sut.GetDefaultPresets()
            .Select(p => p.Id)
            .Except(V1PresetIds)
            .ToList();

        // Act
        await _sut.EnsureDefaultPresetsAsync();
        var profiles = await _sut.GetAllAsync();

        // Assert
        profiles.Select(p => p.Id).Should().BeEquivalentTo(newPresetIds);
    }

    [Fact]
    public async Task EnsureDefaultPresetsAsync_WhenNewPresetIsDeletedAfterSeeding_DoesNotResurrect()
    {
        // Arrange
        await _sut.EnsureDefaultPresetsAsync();
        var seededIds = (await _sut.GetAllAsync()).Select(p => p.Id).ToList();
        var newPresetId = seededIds.Except(V1PresetIds).First();
        (await _sut.DeleteAsync(newPresetId)).Should().BeTrue();

        // Act
        await _sut.EnsureDefaultPresetsAsync();

        // Assert
        (await _sut.GetAllAsync()).Select(p => p.Id).Should().NotContain(newPresetId);
    }

    [Fact]
    public async Task EnsureDefaultPresetsAsync_WhenMarkerIsEmpty_PinsStoreAsUpgraded()
    {
        // Arrange - pre-versioning stores wrote an empty marker file
        await File.WriteAllTextAsync(Path.Combine(_tempDir, ".initialized"), string.Empty);

        // Act
        await _sut.EnsureDefaultPresetsAsync();

        // Assert - the upgrade commits the current seed version
        File.ReadAllText(Path.Combine(_tempDir, ".initialized")).Should().Be("2");
    }

    [Fact]
    public async Task EnsureDefaultPresetsAsync_WhenCuratedStoreCarriesPreVersioningMarker_LeavesStoreUntouched()
    {
        // Arrange - builds before seed versioning wrote an empty marker even for
        // curated stores; a store holding no preset files is still curated
        await File.WriteAllTextAsync(Path.Combine(_tempDir, ".initialized"), string.Empty);
        var customProfile = new SandboxProfile { Name = "Hand Curated" };
        await _sut.SaveAsync(customProfile);

        // Act
        await _sut.EnsureDefaultPresetsAsync();
        var profiles = await _sut.GetAllAsync();

        // Assert
        profiles.Select(p => p.Id).Should().BeEquivalentTo([customProfile.Id]);
        File.ReadAllText(Path.Combine(_tempDir, ".initialized")).Should().Be("2");
    }

    [Fact]
    public async Task EnsureDefaultPresetsAsync_WhenV1UpgradeIsInterrupted_ResumeSeedsNewPresets()
    {
        // Arrange - a crash left .initializing behind before the v1 upgrade finished
        await File.WriteAllTextAsync(Path.Combine(_tempDir, ".initialized"), "1");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, ".initializing"), string.Empty);
        var newPresetIds = _sut.GetDefaultPresets()
            .Select(p => p.Id)
            .Except(V1PresetIds)
            .ToList();

        // Act
        await _sut.EnsureDefaultPresetsAsync();
        var profiles = await _sut.GetAllAsync();

        // Assert
        profiles.Select(p => p.Id).Should().BeEquivalentTo(newPresetIds);
        File.Exists(Path.Combine(_tempDir, ".initializing")).Should().BeFalse();
        File.ReadAllText(Path.Combine(_tempDir, ".initialized")).Should().Be("2");
    }

    [Fact]
    public async Task EnsureDefaultPresetsAsync_WhenMarkerContainsUnparsableText_TreatsStoreAsVersion1()
    {
        // Arrange - garbage marker content falls back to the pre-versioning baseline
        await File.WriteAllTextAsync(Path.Combine(_tempDir, ".initialized"), "not-a-version");

        // Act
        await _sut.EnsureDefaultPresetsAsync();
        var profiles = await _sut.GetAllAsync();

        // Assert - only post-version-1 presets reach the empty store
        profiles.Select(p => p.Id).Should().NotIntersectWith(V1PresetIds);
        profiles.Should().NotBeEmpty();
    }

    [Fact]
    public async Task EnsureDefaultPresetsAsync_WhenMarkerContainsNegativeVersion_SeedsAllPresets()
    {
        // Arrange - a negative (corrupt) version is below every introduced-in level
        await File.WriteAllTextAsync(Path.Combine(_tempDir, ".initialized"), "-1");

        // Act
        await _sut.EnsureDefaultPresetsAsync();
        var profiles = await _sut.GetAllAsync();

        // Assert - the empty store is treated as fresh: every preset is seeded
        profiles.Should().HaveCount(_sut.GetDefaultPresets().Count);
    }

    [Fact]
    public async Task EnsureDefaultPresetsAsync_WhenV1StoreMixesPresetAndCustomProfiles_UpgradesAndKeepsCustom()
    {
        // Arrange - preset-file presence (not mere population) drives the v1 upgrade:
        // a store that still holds a v1 preset plus the user's own profile gets v2 presets
        await File.WriteAllTextAsync(Path.Combine(_tempDir, ".initialized"), "1");
        var v1Preset = _sut.GetDefaultPresets().Single(p => p.Id == V1PresetIds[0]);
        var customProfile = new SandboxProfile { Name = "Custom" };
        await _sut.SaveAsync(v1Preset);
        await _sut.SaveAsync(customProfile);

        // Act
        await _sut.EnsureDefaultPresetsAsync();
        var profiles = (await _sut.GetAllAsync()).ToList();

        // Assert
        var newPresetIds = _sut.GetDefaultPresets().Select(p => p.Id).Except(V1PresetIds).ToList();
        profiles.Select(p => p.Id).Should().Contain(newPresetIds);
        profiles.Should().Contain(p => p.Id == customProfile.Id);
        profiles.Should().Contain(p => p.Id == v1Preset.Id);
    }

    [Fact]
    public async Task EnsureDefaultPresetsAsync_WhenCustomProfileExists_DoesNotSeedPresets()
    {
        var customProfile = new SandboxProfile { Name = "Custom Profile" };
        await _sut.SaveAsync(customProfile);

        await _sut.EnsureDefaultPresetsAsync();
        var profiles = await _sut.GetAllAsync();

        profiles.Select(profile => profile.Id).Should().BeEquivalentTo([customProfile.Id]);
        Directory.GetFiles(_tempDir, "*.json").Should().ContainSingle();
        File.Exists(Path.Combine(_tempDir, ".initialized")).Should().BeTrue();
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
