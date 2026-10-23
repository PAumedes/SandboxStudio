using FluentAssertions;
using SandboxStudio.Core.Capabilities;
using Xunit;

namespace SandboxStudio.Tests.Unit.Capabilities;

public sealed class HostCapabilitiesTests
{
    private static HostCapabilities CreateSut(
        int osBuild = 26100,
        bool isArm64 = false,
        string? editionId = "Professional",
        bool sandboxExecutableExists = true,
        IReadOnlyDictionary<string, object?>? policyValues = null) =>
        new(
            osBuild: () => osBuild,
            isArm64: () => isArm64,
            editionId: () => editionId,
            sandboxExecutableExists: () => sandboxExecutableExists,
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
        var sut = CreateSut(editionId: editionId, sandboxExecutableExists: true);

        // Act & Assert
        sut.SandboxStatus.Should().Be(HostSandboxStatus.UnsupportedEdition);
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
}
