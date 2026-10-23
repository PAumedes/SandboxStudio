using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32;

namespace SandboxStudio.Core.Capabilities;

/// <summary>
/// Computes effective <see cref="SandboxFeature"/> availability from the host OS build
/// and the Windows Sandbox policy values under
/// <c>HKLM\SOFTWARE\Policies\Microsoft\Windows\Sandbox</c>.
/// </summary>
public sealed class HostCapabilities : IHostCapabilities
{
    private const string PolicyKeyPath = @"SOFTWARE\Policies\Microsoft\Windows\Sandbox";

    private static readonly IReadOnlyDictionary<SandboxFeature, FeatureRule> Rules =
        new Dictionary<SandboxFeature, FeatureRule>
        {
            [SandboxFeature.ConfigurationFiles] = new(RequiredBuild: 18342),
            [SandboxFeature.MappedFolderEnvironmentVariables] = new(RequiredBuild: 22631),
            [SandboxFeature.HostMouseSettings] = new(RequiredBuild: 26100),
            [SandboxFeature.WsbCli] = new(RequiredBuild: 26100),
            [SandboxFeature.Networking] = new(PolicyValueName: "AllowNetworking", PolicyApplicableBuild: 19041),
            [SandboxFeature.VGpu] = new(PolicyValueName: "AllowVGPU", PolicyApplicableBuild: 19041),
            [SandboxFeature.AudioInput] = new(PolicyValueName: "AllowAudioInput", PolicyApplicableBuild: 19041),
            [SandboxFeature.VideoInput] = new(PolicyValueName: "AllowVideoInput", PolicyApplicableBuild: 19041),
            [SandboxFeature.PrinterRedirection] = new(PolicyValueName: "AllowPrinterRedirection", PolicyApplicableBuild: 19041),
            [SandboxFeature.ClipboardRedirection] = new(PolicyValueName: "AllowClipboardRedirection", PolicyApplicableBuild: 19041),
            [SandboxFeature.MappedFolders] = new(PolicyValueName: "AllowMappedFolders", PolicyApplicableBuild: 26100),
            [SandboxFeature.WritableMappedFolders] = new(PolicyValueName: "AllowWriteToMappedFolders", PolicyApplicableBuild: 26100),
        };

    private readonly Func<bool> _sandboxExecutableExists;
    private readonly Func<string, object?> _readPolicyValue;

    /// <summary>
    /// Initializes a new instance of <see cref="HostCapabilities"/>.
    /// Probes are injectable to keep the service testable on any machine.
    /// </summary>
    /// <param name="osBuild">Host OS build number probe.</param>
    /// <param name="isArm64">Arm-architecture probe.</param>
    /// <param name="editionId">Windows edition identifier probe.</param>
    /// <param name="sandboxExecutableExists">Windows Sandbox executable presence probe.</param>
    /// <param name="readPolicyValue">Reads a policy value by name; returns <see langword="null"/> when not configured.</param>
    public HostCapabilities(
        Func<int>? osBuild = null,
        Func<bool>? isArm64 = null,
        Func<string?>? editionId = null,
        Func<bool>? sandboxExecutableExists = null,
        Func<string, object?>? readPolicyValue = null)
    {
        var osBuildProbe = osBuild ?? (() => Environment.OSVersion.Version.Build);
        var arm64Probe = isArm64 ?? (() =>
            RuntimeInformation.OSArchitecture is Architecture.Arm or Architecture.Arm64);
        var editionProbe = editionId ?? ReadEditionId;

        OsBuild = osBuildProbe();
        IsArm64 = arm64Probe();
        EditionId = editionProbe();

        _sandboxExecutableExists = sandboxExecutableExists
            ?? (() => File.Exists(SandboxHostPaths.WindowsSandboxExe));
        _readPolicyValue = readPolicyValue ?? ReadPolicyValue;
    }

    /// <inheritdoc/>
    public int OsBuild { get; }

    /// <inheritdoc/>
    public bool IsArm64 { get; }

    /// <inheritdoc/>
    public string? EditionId { get; }

    /// <inheritdoc/>
    public HostSandboxStatus SandboxStatus
    {
        get
        {
            if (EditionId is not null && !IsSupportedEdition(EditionId))
            {
                return HostSandboxStatus.UnsupportedEdition;
            }

            return _sandboxExecutableExists()
                ? HostSandboxStatus.Available
                : HostSandboxStatus.NotInstalled;
        }
    }

    /// <inheritdoc/>
    public FeatureAvailability GetAvailability(SandboxFeature feature)
    {
        var rule = Rules[feature];

        if (rule.RequiredBuild is { } requiredBuild && OsBuild < requiredBuild)
        {
            return new FeatureAvailability(
                FeatureStatus.RequiresNewerWindows,
                $"Requires Windows build {requiredBuild} or later (host build: {OsBuild}).",
                requiredBuild);
        }

        if (rule.PolicyValueName is { } policyValueName &&
            OsBuild >= rule.PolicyApplicableBuild!.Value &&
            IsBlockedByPolicy(policyValueName))
        {
            return new FeatureAvailability(
                FeatureStatus.DisabledByPolicy,
                $"Disabled by host policy '{policyValueName}'.");
        }

        return new FeatureAvailability(FeatureStatus.Available);
    }

    private static bool IsSupportedEdition(string editionId) =>
        editionId.Contains("Professional", StringComparison.OrdinalIgnoreCase) ||
        editionId.Contains("Enterprise", StringComparison.OrdinalIgnoreCase) ||
        editionId.Contains("Education", StringComparison.OrdinalIgnoreCase);

    private bool IsBlockedByPolicy(string valueName) =>
        _readPolicyValue(valueName) is int value && value == 0;

    private static string? ReadEditionId()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            return Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion",
                "EditionID",
                null) as string;
        }
        catch (Exception ex) when (ex is IOException or SecurityException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static object? ReadPolicyValue(string valueName)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(PolicyKeyPath, writable: false);
            return key?.GetValue(valueName);
        }
        catch (Exception ex) when (ex is IOException or SecurityException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <param name="RequiredBuild">Minimum build for OS-gated features, or <see langword="null"/>.</param>
    /// <param name="PolicyValueName">Registry value under the Sandbox policy key, or <see langword="null"/>.</param>
    /// <param name="PolicyApplicableBuild">First build on which the policy takes effect.</param>
    private sealed record FeatureRule(
        int? RequiredBuild = null,
        string? PolicyValueName = null,
        int? PolicyApplicableBuild = null);
}
