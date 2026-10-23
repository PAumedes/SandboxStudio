using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Security;
using Microsoft.Win32;
using SandboxStudio.Core.Models;

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
    private readonly Func<bool> _virtualizationEnabled;
    private readonly Func<string, object?> _readPolicyValue;

    /// <summary>
    /// Initializes a new instance of <see cref="HostCapabilities"/>.
    /// Probes are injectable to keep the service testable on any machine.
    /// </summary>
    /// <param name="osBuild">Host OS build number probe.</param>
    /// <param name="isArm64">Arm-architecture probe.</param>
    /// <param name="editionId">Windows edition identifier probe.</param>
    /// <param name="sandboxExecutableExists">Windows Sandbox executable presence probe.</param>
    /// <param name="virtualizationEnabled">Hardware-virtualization probe; reports disabled on non-Windows hosts (unreached in practice, since the executable check fails first).</param>
    /// <param name="readPolicyValue">Reads a policy value by name; returns <see langword="null"/> when not configured.</param>
    public HostCapabilities(
        Func<int>? osBuild = null,
        Func<bool>? isArm64 = null,
        Func<string?>? editionId = null,
        Func<bool>? sandboxExecutableExists = null,
        Func<string, object?>? readPolicyValue = null,
        Func<bool>? virtualizationEnabled = null)
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
        _virtualizationEnabled = virtualizationEnabled ?? DetectVirtualizationEnabled;
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
            // Executable presence is the ground truth; the edition check only
            // explains why the feature may be missing.
            if (!_sandboxExecutableExists())
            {
                return string.IsNullOrWhiteSpace(EditionId) || IsSupportedEdition(EditionId)
                    ? HostSandboxStatus.NotInstalled
                    : HostSandboxStatus.UnsupportedEdition;
            }

            // The optional feature is installed; the only remaining boot blocker
            // is firmware-level virtualization.
            return _virtualizationEnabled()
                ? HostSandboxStatus.Available
                : HostSandboxStatus.VirtualizationDisabled;
        }
    }

    /// <inheritdoc/>
    public FeatureAvailability GetAvailability(SandboxFeature feature)
    {
        if (!Rules.TryGetValue(feature, out var rule))
        {
            throw new ArgumentOutOfRangeException(nameof(feature), feature, "Unknown sandbox feature.");
        }

        if (rule.RequiredBuild is { } requiredBuild && OsBuild < requiredBuild)
        {
            return new FeatureAvailability(
                FeatureStatus.RequiresNewerWindows,
                $"Requires Windows build {requiredBuild} or later (host build: {OsBuild}).",
                requiredBuild);
        }

        if (rule.PolicyValueName is { } policyValueName &&
            (rule.PolicyApplicableBuild is not { } applicableBuild || OsBuild >= applicableBuild) &&
            IsBlockedByPolicy(policyValueName))
        {
            return new FeatureAvailability(
                FeatureStatus.DisabledByPolicy,
                $"Disabled by host policy '{policyValueName}'.");
        }

        return new FeatureAvailability(FeatureStatus.Available);
    }

    /// <inheritdoc/>
    public IReadOnlyList<PolicyOverride> GetPolicyOverrides(WsbConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var overrides = new List<PolicyOverride>();

        CheckTriState(configuration.Networking, SandboxFeature.Networking, nameof(configuration.Networking), defaultEnabled: true);
        CheckTriState(configuration.VGpu, SandboxFeature.VGpu, nameof(configuration.VGpu), defaultEnabled: !IsArm64);
        CheckTriState(configuration.AudioInput, SandboxFeature.AudioInput, nameof(configuration.AudioInput), defaultEnabled: true);
        CheckTriState(configuration.VideoInput, SandboxFeature.VideoInput, nameof(configuration.VideoInput), defaultEnabled: false);
        CheckTriState(configuration.PrinterRedirection, SandboxFeature.PrinterRedirection, nameof(configuration.PrinterRedirection), defaultEnabled: false);
        CheckTriState(configuration.ClipboardRedirection, SandboxFeature.ClipboardRedirection, nameof(configuration.ClipboardRedirection), defaultEnabled: true);

        if (configuration.MappedFolders is { Count: > 0 })
        {
            AddIfBlocked(
                SandboxFeature.MappedFolders,
                nameof(configuration.MappedFolders),
                policy => $"{nameof(configuration.MappedFolders)} has no effect because host policy '{policy}' disables it.");

            if (configuration.MappedFolders.Any(folder => !folder.IsReadOnly))
            {
                AddIfBlocked(
                    SandboxFeature.WritableMappedFolders,
                    nameof(configuration.MappedFolders),
                    policy => $"Writable mapped folders have no effect because host policy '{policy}' disables write access.");
            }
        }

        return overrides;

        void CheckTriState(string? configured, SandboxFeature feature, string settingName, bool defaultEnabled)
        {
            // "Enable" requests the feature explicitly; an omitted element or "Default"
            // requests it implicitly whenever the feature defaults to enabled on this host.
            var requestsFeature = configured is "Enable" ||
                ((configured is null or "Default") && defaultEnabled);
            if (!requestsFeature)
            {
                return;
            }

            AddIfBlocked(feature, settingName, policy => configured is null
                ? $"{settingName} is not set and defaults to enabled, but host policy '{policy}' disables it."
                : $"{settingName} is set to '{configured}' but host policy '{policy}' disables it.");
        }

        void AddIfBlocked(SandboxFeature feature, string settingName, Func<string, string> detail)
        {
            if (GetAvailability(feature).Status != FeatureStatus.DisabledByPolicy)
            {
                return;
            }

            var policyName = Rules[feature].PolicyValueName!;
            overrides.Add(new PolicyOverride(settingName, policyName, detail(policyName)));
        }
    }

    private static bool IsSupportedEdition(string editionId) =>
        editionId.Contains("Professional", StringComparison.OrdinalIgnoreCase) ||
        editionId.Contains("Enterprise", StringComparison.OrdinalIgnoreCase) ||
        editionId.Contains("Education", StringComparison.OrdinalIgnoreCase);

    private bool IsBlockedByPolicy(string valueName) =>
        _readPolicyValue(valueName) is int value && value == 0;

    /// <summary>
    /// Win32 PF_VIRT_FIRMWARE_ENABLED: hardware virtualization is enabled in firmware.
    /// </summary>
    private const uint PfVirtFirmwareEnabled = 21;

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern int IsProcessorFeaturePresent(uint processorFeature);

    private static bool DetectVirtualizationEnabled()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        // PF_VIRT_FIRMWARE_ENABLED goes false once a hypervisor runs (Hyper-V,
        // WSL2, VBS); an active hypervisor is itself proof that hardware
        // virtualization is enabled.
        return IsProcessorFeaturePresent(PfVirtFirmwareEnabled) != 0 || IsHypervisorPresent();
    }

    /// <summary>
    /// CPUID leaf 1 ECX bit 31 reports that a hypervisor is present (x86/x64 only;
    /// reports false on Arm64 where the firmware flag remains the signal).
    /// </summary>
    private static bool IsHypervisorPresent() =>
        X86Base.IsSupported && (X86Base.CpuId(1, 0).Ecx & (1 << 31)) != 0;

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
