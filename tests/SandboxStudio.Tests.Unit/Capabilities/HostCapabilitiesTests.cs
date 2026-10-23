using FluentAssertions;
using SandboxStudio.Core.Capabilities;
using SandboxStudio.Core.Models;
using Xunit;

namespace SandboxStudio.Tests.Unit.Capabilities;

public sealed class HostCapabilitiesTests
{
    private static HostCapabilities CreateSut(
        int osBuild = 26100,
        bool isArm64 = false,
        string? editionId = "Professional",
        bool sandboxExecutableExists = true,
        bool virtualizationEnabled = true,
        IReadOnlyDictionary<string, object?>? policyValues = null) =>
        new(
            osBuild: () => osBuild,
            isArm64: () => isArm64,
            editionId: () => editionId,
            sandboxExecutableExists: () => sandboxExecutableExists,
            virtualizationEnabled: () => virtualizationEnabled,
            readPolicyValue: name => policyValues is not null && policyValues.TryGetValue(name, out var value) ? value : null);

    [Fact]
    public void Constructor_ExposesProbedHostFacts()
    {
        // Arrange & Act
        var sut = CreateSut(osBuild: 22631, isArm64: true, editionId: "EnterpriseN");

        // Assert
        sut.OsBuild.Should().Be(22631);
        sut.IsArm64.Should().BeTrue();
        sut.EditionId.Should().Be("EnterpriseN");
    }

    [Fact]
    public void GetAvailability_WhenBuildIsBelowRequirement_ReturnsRequiresNewerWindows()
    {
        // Arrange
        var sut = CreateSut(osBuild: 22631);

        // Act
        var result = sut.GetAvailability(SandboxFeature.WsbCli);

        // Assert
        result.Status.Should().Be(FeatureStatus.RequiresNewerWindows);
        result.RequiredBuild.Should().Be(26100);
        result.Reason.Should().Contain("26100").And.Contain("22631");
    }

    [Theory]
    [InlineData(26100)]
    [InlineData(26200)]
    public void GetAvailability_WhenBuildMeetsRequirement_ReturnsAvailable(int osBuild)
    {
        // Arrange
        var sut = CreateSut(osBuild: osBuild);

        // Act
        var result = sut.GetAvailability(SandboxFeature.WsbCli);

        // Assert
        result.Status.Should().Be(FeatureStatus.Available);
        result.Reason.Should().BeNull();
    }

    [Fact]
    public void GetAvailability_WhenPolicyDisablesFeatureOnApplicableBuild_ReturnsDisabledByPolicy()
    {
        // Arrange
        var sut = CreateSut(
            osBuild: 26100,
            policyValues: new Dictionary<string, object?> { ["AllowNetworking"] = 0 });

        // Act
        var result = sut.GetAvailability(SandboxFeature.Networking);

        // Assert
        result.Status.Should().Be(FeatureStatus.DisabledByPolicy);
        result.Reason.Should().Contain("AllowNetworking");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    public void GetAvailability_WhenPolicyAllowsFeature_ReturnsAvailable(int policyValue)
    {
        // Arrange
        var sut = CreateSut(
            osBuild: 26100,
            policyValues: new Dictionary<string, object?> { ["AllowNetworking"] = policyValue });

        // Act
        var result = sut.GetAvailability(SandboxFeature.Networking);

        // Assert
        result.Status.Should().Be(FeatureStatus.Available);
    }

    [Fact]
    public void GetAvailability_WhenPolicyIsNotConfigured_ReturnsAvailable()
    {
        // Arrange
        var sut = CreateSut(osBuild: 26100, policyValues: new Dictionary<string, object?>());

        // Act
        var result = sut.GetAvailability(SandboxFeature.VGpu);

        // Assert
        result.Status.Should().Be(FeatureStatus.Available);
    }

    [Fact]
    public void GetAvailability_WhenBuildPredatesPolicy_IgnoresDisabledValue()
    {
        // Arrange - AllowMappedFolders cannot take effect before 24H2
        var sut = CreateSut(
            osBuild: 22631,
            policyValues: new Dictionary<string, object?> { ["AllowMappedFolders"] = 0 });

        // Act
        var result = sut.GetAvailability(SandboxFeature.MappedFolders);

        // Assert
        result.Status.Should().Be(FeatureStatus.Available);
    }

    [Theory]
    [InlineData(SandboxFeature.MappedFolders, "AllowMappedFolders")]
    [InlineData(SandboxFeature.WritableMappedFolders, "AllowWriteToMappedFolders")]
    [InlineData(SandboxFeature.VGpu, "AllowVGPU")]
    [InlineData(SandboxFeature.AudioInput, "AllowAudioInput")]
    [InlineData(SandboxFeature.VideoInput, "AllowVideoInput")]
    [InlineData(SandboxFeature.PrinterRedirection, "AllowPrinterRedirection")]
    [InlineData(SandboxFeature.ClipboardRedirection, "AllowClipboardRedirection")]
    public void GetAvailability_MapsEachFeatureToItsPolicyValue(SandboxFeature feature, string policyName)
    {
        // Arrange
        var sut = CreateSut(
            osBuild: 26100,
            policyValues: new Dictionary<string, object?> { [policyName] = 0 });

        // Act
        var result = sut.GetAvailability(feature);

        // Assert
        result.Status.Should().Be(FeatureStatus.DisabledByPolicy);
        result.Reason.Should().Contain(policyName);
    }

    [Fact]
    public void GetAvailability_EveryFeatureHasARule()
    {
        // Arrange
        var sut = CreateSut();

        // Act & Assert
        foreach (var feature in Enum.GetValues<SandboxFeature>())
        {
            var act = () => sut.GetAvailability(feature);
            act.Should().NotThrow($"every {nameof(SandboxFeature)} must resolve");
        }
    }

    [Theory]
    [InlineData("Core")]
    [InlineData("CoreSingleLanguage")]
    [InlineData("CoreConnected")]
    public void SandboxStatus_WhenEditionDoesNotSupportSandbox_ReturnsUnsupportedEdition(string editionId)
    {
        // Arrange
        var sut = CreateSut(editionId: editionId, sandboxExecutableExists: false);

        // Act & Assert
        sut.SandboxStatus.Should().Be(HostSandboxStatus.UnsupportedEdition);
    }

    [Fact]
    public void SandboxStatus_WhenExecutableExistsOnUnsupportedEdition_ExecutableWins()
    {
        // Arrange - exe presence is the ground truth; a misdetected edition must not
        // report Sandbox as unusable when it is actually installed
        var sut = CreateSut(editionId: "Core", sandboxExecutableExists: true);

        // Act & Assert
        sut.SandboxStatus.Should().Be(HostSandboxStatus.Available);
    }

    [Fact]
    public void SandboxStatus_WhenEditionIsEmpty_FallsBackToExecutableProbe()
    {
        // Arrange
        var sut = CreateSut(editionId: "", sandboxExecutableExists: false);

        // Act & Assert
        sut.SandboxStatus.Should().Be(HostSandboxStatus.NotInstalled);
    }

    [Theory]
    [InlineData("Professional")]
    [InlineData("ProfessionalWorkstation")]
    [InlineData("Enterprise")]
    [InlineData("EnterpriseN")]
    [InlineData("Education")]
    [InlineData("IoTEnterprise")]
    public void SandboxStatus_WhenEditionSupportsSandboxAndExeExists_ReturnsAvailable(string editionId)
    {
        // Arrange
        var sut = CreateSut(editionId: editionId, sandboxExecutableExists: true);

        // Act & Assert
        sut.SandboxStatus.Should().Be(HostSandboxStatus.Available);
    }

    [Fact]
    public void SandboxStatus_WhenExecutableIsMissing_ReturnsNotInstalled()
    {
        // Arrange
        var sut = CreateSut(editionId: "Professional", sandboxExecutableExists: false);

        // Act & Assert
        sut.SandboxStatus.Should().Be(HostSandboxStatus.NotInstalled);
    }

    [Fact]
    public void SandboxStatus_WhenEditionIsUnknown_FallsBackToExecutableProbe()
    {
        // Arrange
        var sut = CreateSut(editionId: null, sandboxExecutableExists: true);

        // Act & Assert
        sut.SandboxStatus.Should().Be(HostSandboxStatus.Available);
    }

    [Fact]
    public void SandboxStatus_WhenVirtualizationIsDisabled_ReturnsVirtualizationDisabled()
    {
        // Arrange
        var sut = CreateSut(sandboxExecutableExists: true, virtualizationEnabled: false);

        // Act & Assert
        sut.SandboxStatus.Should().Be(HostSandboxStatus.VirtualizationDisabled);
    }

    [Fact]
    public void SandboxStatus_WhenVirtualizationIsDisabled_EditionStillDoesNotMatter()
    {
        // Arrange - the executable is present, so the unsupported edition cannot downgrade the verdict
        var sut = CreateSut(
            editionId: "Home",
            sandboxExecutableExists: true,
            virtualizationEnabled: false);

        // Act & Assert
        sut.SandboxStatus.Should().Be(HostSandboxStatus.VirtualizationDisabled);
    }

    [Fact]
    public void SandboxStatus_WhenExeMissingAndEditionUnsupported_VirtualizationCannotLeakIntoVerdict()
    {
        // Arrange - virt is only consulted once the executable exists; absent feature + bad edition stays an edition verdict
        var sut = CreateSut(
            editionId: "Home",
            sandboxExecutableExists: false,
            virtualizationEnabled: false);

        // Act & Assert
        sut.SandboxStatus.Should().Be(HostSandboxStatus.UnsupportedEdition);
    }

    [Fact]
    public void SandboxStatus_WhenExecutableIsMissing_VirtualizationProbeIsNotConsulted()
    {
        // Arrange - the probe must not run when there is nothing to launch anyway
        var sut = new HostCapabilities(
            osBuild: () => 26100,
            isArm64: () => false,
            editionId: () => "Professional",
            sandboxExecutableExists: () => false,
            virtualizationEnabled: () => throw new InvalidOperationException("probe must not run"),
            readPolicyValue: _ => null);

        // Act & Assert
        sut.SandboxStatus.Should().Be(HostSandboxStatus.NotInstalled);
    }

    [Fact]
    public void GetPolicyOverrides_WhenEnabledSettingIsBlocked_ReturnsOverride()
    {
        // Arrange
        var sut = CreateSut(policyValues: new Dictionary<string, object?> { ["AllowNetworking"] = 0 });
        var config = new WsbConfiguration { Networking = "Enable" };

        // Act
        var overrides = sut.GetPolicyOverrides(config);

        // Assert
        var policyOverride = overrides.Should().ContainSingle().Which;
        policyOverride.SettingName.Should().Be(nameof(WsbConfiguration.Networking));
        policyOverride.PolicyValueName.Should().Be("AllowNetworking");
        policyOverride.Message.Should().Contain("Enable").And.Contain("AllowNetworking");
    }

    [Fact]
    public void GetPolicyOverrides_WhenDisabledSettingIsBlocked_ReturnsNoOverride()
    {
        // Arrange - a blocked feature stays blocked; "Disable" is not overridden
        var sut = CreateSut(policyValues: new Dictionary<string, object?> { ["AllowNetworking"] = 0 });
        var config = new WsbConfiguration { Networking = "Disable" };

        // Act
        var overrides = sut.GetPolicyOverrides(config);

        // Assert
        overrides.Should().BeEmpty();
    }

    [Fact]
    public void GetPolicyOverrides_WhenDefaultWouldEnableABlockedFeature_ReturnsOverride()
    {
        // Arrange - networking is enabled by default, so "Default" is still overridden
        var sut = CreateSut(policyValues: new Dictionary<string, object?> { ["AllowNetworking"] = 0 });
        var config = new WsbConfiguration { Networking = "Default" };

        // Act
        var overrides = sut.GetPolicyOverrides(config);

        // Assert
        overrides.Should().ContainSingle(o => o.SettingName == nameof(WsbConfiguration.Networking));
    }

    [Fact]
    public void GetPolicyOverrides_WhenDefaultWouldDisableABlockedFeature_ReturnsNoOverride()
    {
        // Arrange - video input is disabled by default, so "Default" changes nothing
        var sut = CreateSut(policyValues: new Dictionary<string, object?> { ["AllowVideoInput"] = 0 });
        var config = new WsbConfiguration { VideoInput = "Default" };

        // Act
        var overrides = sut.GetPolicyOverrides(config);

        // Assert
        overrides.Should().BeEmpty();
    }

    [Fact]
    public void GetPolicyOverrides_VGpuDefaultOnArm64_IsNotOverriddenByPolicy()
    {
        // Arrange - vGPU defaults to disabled on Arm64, so "Default" changes nothing
        var sut = CreateSut(
            isArm64: true,
            policyValues: new Dictionary<string, object?> { ["AllowVGPU"] = 0 });
        var config = new WsbConfiguration { VGpu = "Default" };

        // Act
        var overrides = sut.GetPolicyOverrides(config);

        // Assert
        overrides.Should().BeEmpty();
    }

    [Fact]
    public void GetPolicyOverrides_VGpuDefaultOnX64_IsOverriddenByPolicy()
    {
        // Arrange - vGPU defaults to enabled on non-Arm64 devices
        var sut = CreateSut(policyValues: new Dictionary<string, object?> { ["AllowVGPU"] = 0 });
        var config = new WsbConfiguration { VGpu = "Default" };

        // Act
        var overrides = sut.GetPolicyOverrides(config);

        // Assert
        overrides.Should().ContainSingle(o => o.SettingName == nameof(WsbConfiguration.VGpu));
    }

    [Fact]
    public void GetPolicyOverrides_WhenMappedFoldersAreBlocked_ReturnsOverride()
    {
        // Arrange
        var sut = CreateSut(policyValues: new Dictionary<string, object?> { ["AllowMappedFolders"] = 0 });
        var config = new WsbConfiguration
        {
            MappedFolders = { new WsbMappedFolder { HostFolder = @"C:\Share", IsReadOnly = true } }
        };

        // Act
        var overrides = sut.GetPolicyOverrides(config);

        // Assert
        overrides.Should().ContainSingle(o => o.SettingName == nameof(WsbConfiguration.MappedFolders));
    }

    [Fact]
    public void GetPolicyOverrides_WhenOnlyWritesAreBlocked_ReadOnlyMappingsAreNotOverridden()
    {
        // Arrange
        var sut = CreateSut(policyValues: new Dictionary<string, object?> { ["AllowWriteToMappedFolders"] = 0 });
        var config = new WsbConfiguration
        {
            MappedFolders = { new WsbMappedFolder { HostFolder = @"C:\Share", IsReadOnly = true } }
        };

        // Act
        var overrides = sut.GetPolicyOverrides(config);

        // Assert
        overrides.Should().BeEmpty();
    }

    [Fact]
    public void GetPolicyOverrides_WhenWritesAreBlocked_WritableMappingsAreOverridden()
    {
        // Arrange
        var sut = CreateSut(policyValues: new Dictionary<string, object?> { ["AllowWriteToMappedFolders"] = 0 });
        var config = new WsbConfiguration
        {
            MappedFolders = { new WsbMappedFolder { HostFolder = @"C:\Share", IsReadOnly = false } }
        };

        // Act
        var overrides = sut.GetPolicyOverrides(config);

        // Assert
        overrides.Should().ContainSingle(o => o.PolicyValueName == "AllowWriteToMappedFolders");
    }

    [Fact]
    public void GetPolicyOverrides_WhenNothingIsBlocked_ReturnsEmpty()
    {
        // Arrange
        var sut = CreateSut(policyValues: new Dictionary<string, object?>());
        var config = new WsbConfiguration
        {
            Networking = "Enable",
            MemoryInMB = 4096
        };

        // Act
        var overrides = sut.GetPolicyOverrides(config);

        // Assert
        overrides.Should().BeEmpty();
    }

    [Fact]
    public void GetPolicyOverrides_WhenConfigurationIsNull_Throws()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var act = () => sut.GetPolicyOverrides(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetPolicyOverrides_WhenUnsetSettingDefaultsToEnabled_ReturnsOverride()
    {
        // Arrange - an omitted element behaves like "Default": networking is enabled
        // by default, so a null value is still overridden by policy
        var sut = CreateSut(policyValues: new Dictionary<string, object?> { ["AllowNetworking"] = 0 });
        var config = new WsbConfiguration();

        // Act
        var overrides = sut.GetPolicyOverrides(config);

        // Assert
        overrides.Should().ContainSingle(o =>
            o.SettingName == nameof(WsbConfiguration.Networking) &&
            o.Message.Contains("not set"));
    }

    [Fact]
    public void GetPolicyOverrides_WhenUnsetSettingDefaultsToDisabled_ReturnsNoOverride()
    {
        // Arrange - video input defaults to disabled, so an omitted element changes nothing
        var sut = CreateSut(policyValues: new Dictionary<string, object?> { ["AllowVideoInput"] = 0 });
        var config = new WsbConfiguration();

        // Act
        var overrides = sut.GetPolicyOverrides(config);

        // Assert
        overrides.Should().BeEmpty();
    }

    [Fact]
    public void GetPolicyOverrides_WhenMultiplePoliciesBlock_ReturnsOneEntryPerSetting()
    {
        // Arrange
        var sut = CreateSut(policyValues: new Dictionary<string, object?>
        {
            ["AllowNetworking"] = 0,
            ["AllowClipboardRedirection"] = 0
        });
        var config = new WsbConfiguration
        {
            Networking = "Enable",
            ClipboardRedirection = "Enable"
        };

        // Act
        var overrides = sut.GetPolicyOverrides(config);

        // Assert
        overrides.Should().HaveCount(2);
        overrides.Should().Contain(o => o.PolicyValueName == "AllowNetworking");
        overrides.Should().Contain(o => o.PolicyValueName == "AllowClipboardRedirection");
    }

    [Theory]
    [InlineData("0")]        // REG_SZ is not REG_DWORD
    [InlineData(0L)]         // REG_QWORD is not REG_DWORD
    public void GetAvailability_WhenPolicyValueIsNotDword_TreatsAsNotBlocked(object policyValue)
    {
        // Arrange - only a DWORD 0 blocks; other registry kinds are not honored by policy
        var sut = CreateSut(
            osBuild: 26100,
            policyValues: new Dictionary<string, object?> { ["AllowNetworking"] = policyValue });

        // Act
        var result = sut.GetAvailability(SandboxFeature.Networking);

        // Assert
        result.Status.Should().Be(FeatureStatus.Available);
    }

    [Theory]
    [InlineData(19040, FeatureStatus.Available)]
    [InlineData(19041, FeatureStatus.DisabledByPolicy)]
    public void GetAvailability_EnforcesClassicPolicyFloorAt19041(int osBuild, FeatureStatus expected)
    {
        // Arrange - the six classic Sandbox policies take effect on Win10 2004 / Win11 21H2
        var sut = CreateSut(
            osBuild: osBuild,
            policyValues: new Dictionary<string, object?> { ["AllowNetworking"] = 0 });

        // Act
        var result = sut.GetAvailability(SandboxFeature.Networking);

        // Assert
        result.Status.Should().Be(expected);
    }

    [Theory]
    [InlineData(18341, FeatureStatus.RequiresNewerWindows)]
    [InlineData(18342, FeatureStatus.Available)]
    public void GetAvailability_ConfigurationFilesBoundaryAt18342(int osBuild, FeatureStatus expected)
    {
        // Arrange
        var sut = CreateSut(osBuild: osBuild);

        // Act
        var result = sut.GetAvailability(SandboxFeature.ConfigurationFiles);

        // Assert
        result.Status.Should().Be(expected);
    }

    [Theory]
    [InlineData(22630, FeatureStatus.RequiresNewerWindows)]
    [InlineData(22631, FeatureStatus.Available)]
    public void GetAvailability_EnvironmentVariablePathsBoundaryAt22631(int osBuild, FeatureStatus expected)
    {
        // Arrange
        var sut = CreateSut(osBuild: osBuild);

        // Act
        var result = sut.GetAvailability(SandboxFeature.MappedFolderEnvironmentVariables);

        // Assert
        result.Status.Should().Be(expected);
    }

    [Fact]
    public void GetAvailability_WhenFeatureIsUnknown_ThrowsArgumentOutOfRange()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var act = () => sut.GetAvailability((SandboxFeature)999);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
