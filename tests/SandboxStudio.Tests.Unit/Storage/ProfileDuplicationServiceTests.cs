using FluentAssertions;
using Moq;
using SandboxStudio.Core.Models;
using SandboxStudio.Core.Storage;
using Xunit;

namespace SandboxStudio.Tests.Unit.Storage;

public sealed class ProfileDuplicationServiceTests
{
    private readonly Mock<IProfileRepository> _mockRepository = new();
    private readonly ProfileDuplicationService _sut;

    public ProfileDuplicationServiceTests()
    {
        _sut = new ProfileDuplicationService(_mockRepository.Object);
    }

    [Fact]
    public async Task DuplicateAsync_WhenSourceProfileDoesNotExist_ReturnsNullAndDoesNotSave()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SandboxProfile?)null);

        // Act
        var result = await _sut.DuplicateAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
        _mockRepository.Verify(
            r => r.SaveAsync(It.IsAny<SandboxProfile>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DuplicateAsync_CreatesSavedCopyWithNewIdentityAndResetLaunchState()
    {
        // Arrange
        var source = CreateSourceProfile();
        SetupSource(source);

        // Act
        var before = DateTimeOffset.UtcNow;
        var result = await _sut.DuplicateAsync(source.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().NotBe(source.Id);
        result.Id.Should().NotBeEmpty();
        result.CreatedAt.Should().BeOnOrAfter(before);
        result.LastLaunchedAt.Should().BeNull();
        result.IsFavorite.Should().BeFalse();
        result.Description.Should().Be(source.Description);
        result.Tags.Should().Equal(source.Tags);
        result.IconGlyph.Should().Be(source.IconGlyph);
        _mockRepository.Verify(r => r.SaveAsync(result, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DuplicateAsync_DeepCopiesConfiguration()
    {
        // Arrange
        var source = CreateSourceProfile();
        SetupSource(source);

        // Act
        var result = await _sut.DuplicateAsync(source.Id);

        // Assert
        result!.Configuration.Should().NotBeSameAs(source.Configuration);
        result.Configuration.MappedFolders.Should().NotBeSameAs(source.Configuration.MappedFolders);
        result.Configuration.MappedFolders[0].Should().NotBeSameAs(source.Configuration.MappedFolders[0]);
        result.Configuration.LogonCommand.Should().NotBeSameAs(source.Configuration.LogonCommand);
        result.Configuration.MemoryInMB.Should().Be(source.Configuration.MemoryInMB);
        result.Configuration.MappedFolders[0].HostFolder.Should().Be(@"C:\Projects");
        result.Configuration.LogonCommand!.Command.Should().Be("start.exe");
    }

    [Fact]
    public async Task DuplicateAsync_AppendsCopySuffixToName()
    {
        // Arrange
        var source = CreateSourceProfile();
        SetupSource(source, "Dev Box");

        // Act
        var result = await _sut.DuplicateAsync(source.Id);

        // Assert
        result!.Name.Should().Be("Web Lab (copy)");
    }

    [Fact]
    public async Task DuplicateAsync_WhenCopyNameExists_UsesLowestFreeNumberedSuffix()
    {
        // Arrange - "(copy 3)" is taken but "(copy 2)" is free; the lowest gap wins
        var source = CreateSourceProfile();
        SetupSource(source, "Web Lab (copy)", "Web Lab (copy 3)", "Other");

        // Act
        var result = await _sut.DuplicateAsync(source.Id);

        // Assert
        result!.Name.Should().Be("Web Lab (copy 2)");
    }

    [Fact]
    public async Task DuplicateAsync_WhenSourceNameAlreadyHasCopySuffix_ReusesBaseName()
    {
        // Arrange - duplicating a copy keeps numbering on the original base name
        var source = CreateSourceProfile();
        source.Name = "Web Lab (copy 2)";
        SetupSource(source, "Web Lab", "Web Lab (copy)");

        // Act
        var result = await _sut.DuplicateAsync(source.Id);

        // Assert
        result!.Name.Should().Be("Web Lab (copy 3)");
    }

    [Fact]
    public async Task DuplicateAsync_TreatsExistingNamesCaseInsensitively()
    {
        // Arrange
        var source = CreateSourceProfile();
        SetupSource(source, "WEB LAB (COPY)");

        // Act
        var result = await _sut.DuplicateAsync(source.Id);

        // Assert
        result!.Name.Should().Be("Web Lab (copy 2)");
    }

    [Fact]
    public async Task DuplicateAsync_WhenSourceNameHasUpperCaseCopySuffix_StripsForBase()
    {
        // Arrange - the suffix strip must match the source's own suffix case-insensitively
        var source = CreateSourceProfile();
        source.Name = "Web Lab (COPY 5)";
        SetupSource(source, "Web Lab");

        // Act
        var result = await _sut.DuplicateAsync(source.Id);

        // Assert
        result!.Name.Should().Be("Web Lab (copy)");
    }

    [Fact]
    public async Task DuplicateAsync_WhenStoredProfileDataIsIncomplete_ThrowsAndDoesNotSave()
    {
        // Arrange - corrupt-but-loadable JSON can leave required members null
        var source = CreateSourceProfile();
        source.Tags = null!;
        SetupSource(source);

        // Act
        var act = () => _sut.DuplicateAsync(source.Id);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        _mockRepository.Verify(
            r => r.SaveAsync(It.IsAny<SandboxProfile>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DuplicateAsync_ForwardsCancellationToken()
    {
        // Arrange
        var source = CreateSourceProfile();
        SetupSource(source);
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        // Act
        await _sut.DuplicateAsync(source.Id, token);

        // Assert
        _mockRepository.Verify(r => r.GetByIdAsync(source.Id, token), Times.Once);
        _mockRepository.Verify(r => r.GetAllAsync(token), Times.Once);
        _mockRepository.Verify(r => r.SaveAsync(It.IsAny<SandboxProfile>(), token), Times.Once);
    }

    private void SetupSource(SandboxProfile source, params string[] existingNames)
    {
        _mockRepository
            .Setup(r => r.GetByIdAsync(source.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(source);
        _mockRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingNames
                .Append(source.Name)
                .Select(name => new SandboxProfile { Name = name })
                .ToList());
    }

    private static SandboxProfile CreateSourceProfile() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Web Lab",
        Description = "Isolated browsing",
        Tags = new() { "Web" },
        IconGlyph = "\uE774",
        IsFavorite = true,
        CreatedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
        LastLaunchedAt = new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero),
        Configuration = new WsbConfiguration
        {
            Networking = "Enable",
            MemoryInMB = 4096,
            MappedFolders = new()
            {
                new WsbMappedFolder { HostFolder = @"C:\Projects", ReadOnly = "true" }
            },
            LogonCommand = new WsbLogonCommand { Command = "start.exe" }
        }
    };
}
