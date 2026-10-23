using FluentAssertions;
using SandboxStudio.Core.Capabilities;
using SandboxStudio.Core.Presets;
using SandboxStudio.Core.Validation;
using Xunit;

namespace SandboxStudio.Tests.Unit.Presets;

public sealed class CuratedPresetsTests
{
    private static readonly Guid LeftHandedDesktopId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private static CuratedPresets CreateSut(int osBuild) =>
        new(new HostCapabilities(osBuild: () => osBuild));

    [Fact]
    public void GetDefaultPresets_On24H2Host_ExcludesLeftHandedDesktop()
    {
        // Arrange — 24H2 applies host mouse settings automatically; the swap preset is pointless
        var sut = CreateSut(osBuild: 26100);

        // Act
        var presets = sut.GetDefaultPresets();

        // Assert
        presets.Should().HaveCount(5);
        presets.Should().NotContain(p => p.Id == LeftHandedDesktopId);
    }

    [Fact]
    public void GetDefaultPresets_OnPre24H2Host_IncludesLeftHandedDesktop()
    {
        // Arrange — pre-24H2 hosts must swap mouse buttons manually via a logon command
        var sut = CreateSut(osBuild: 22631);

        // Act
        var presets = sut.GetDefaultPresets();

        // Assert
        presets.Should().HaveCount(6);
        var preset = presets.Should().ContainSingle(p => p.Id == LeftHandedDesktopId).Subject;
        preset.Name.Should().Be("Left-Handed Desktop");
    }

    [Fact]
    public void GetDefaultPresets_LeftHandedDesktop_SwapsMouseButtonsAtLogon()
    {
        // Arrange
        var sut = CreateSut(osBuild: 22631);

        // Act
        var preset = sut.GetDefaultPresets().Single(p => p.Id == LeftHandedDesktopId);

        // Assert
        preset.Configuration.LogonCommand.Should().NotBeNull();
        preset.Configuration.LogonCommand!.Command.Should().Contain("SwapMouseButton");
    }

    [Fact]
    public void GetDefaultPresets_LeftHandedDesktop_PassesValidation()
    {
        // Arrange
        var sut = CreateSut(osBuild: 22631);
        var validator = new WsbValidator();

        // Act
        var preset = sut.GetDefaultPresets().Single(p => p.Id == LeftHandedDesktopId);

        // Assert
        validator.Validate(preset.Configuration).IsValid.Should().BeTrue();
    }

    [Fact]
    public void IntroducedInVersions_LeftHandedDesktop_WasIntroducedInVersion3()
    {
        // Assert
        CuratedPresets.IntroducedInVersions.Should().Contain(LeftHandedDesktopId, 3);
    }
}
