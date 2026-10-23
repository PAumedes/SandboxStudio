using SandboxStudio.Core.Capabilities;
using SandboxStudio.Core.Models;

namespace SandboxStudio.Core.Presets;

/// <summary>
/// The built-in, curated profile presets and the seed version in which each was
/// introduced. The list is adapted to the host: presets that only make sense on
/// specific builds (for example, manual mouse-button swapping on pre-24H2 hosts,
/// which do not apply host mouse settings automatically) are omitted elsewhere.
/// </summary>
public sealed class CuratedPresets
{
    /// <summary>
    /// Current preset seed version; bump when presets are added so existing
    /// stores receive only the newly introduced ones.
    /// </summary>
    public const int CurrentSeedVersion = 3;

    /// <summary>
    /// The seed version in which each built-in preset was introduced, keyed by profile id.
    /// </summary>
    public static readonly IReadOnlyDictionary<Guid, int> IntroducedInVersions =
        new Dictionary<Guid, int>
        {
            [Guid.Parse("11111111-1111-1111-1111-111111111111")] = 1,
            [Guid.Parse("22222222-2222-2222-2222-222222222222")] = 1,
            [Guid.Parse("33333333-3333-3333-3333-333333333333")] = 1,
            [Guid.Parse("44444444-4444-4444-4444-444444444444")] = 2,
            [Guid.Parse("55555555-5555-5555-5555-555555555555")] = 2,
            [Guid.Parse("66666666-6666-6666-6666-666666666666")] = 3,
        };

    private readonly IHostCapabilities _capabilities;

    /// <summary>
    /// Initializes a new instance of <see cref="CuratedPresets"/>.
    /// </summary>
    /// <param name="capabilities">Host capability probe used to exclude presets that do not apply to this host.</param>
    public CuratedPresets(IHostCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        _capabilities = capabilities;
    }

    /// <summary>
    /// Returns the curated presets applicable to this host's Windows build.
    /// </summary>
    public IReadOnlyList<SandboxProfile> GetDefaultPresets()
    {
        var presets = new List<SandboxProfile>
        {
            new()
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Air-Gapped Malware Lab",
                Description = "Completely isolated environment with network disabled and clipboard redirection blocked.",
                Tags = new() { "Security", "Malware", "Isolated" },
                IconGlyph = "\uE72E", // Lock icon
                Configuration = new WsbConfiguration
                {
                    Networking = "Disable",
                    VGpu = "Disable",
                    ClipboardRedirection = "Disable",
                    AudioInput = "Disable",
                    VideoInput = "Disable",
                    ProtectedClient = "Enable"
                }
            },
            new()
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Name = "Ephemeral Web Browser",
                Description = "Clean, zero-residual web browsing sandbox with hardware GPU acceleration.",
                Tags = new() { "Privacy", "Web", "Safe" },
                IconGlyph = "\uE774", // Globe icon
                Configuration = new WsbConfiguration
                {
                    Networking = "Enable",
                    VGpu = "Enable",
                    MemoryInMB = 4096,
                    LogonCommand = new WsbLogonCommand
                    {
                        Command = "cmd.exe /c start msedge.exe --inprivate"
                    }
                }
            },
            new()
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Name = "Developer Workstation",
                Description = "Auto-installs Git and VS Code on boot via Winget with 8GB RAM allocated.",
                Tags = new() { "Development", "Coding", "Tools" },
                IconGlyph = "\uE7BE", // Code icon
                Configuration = new WsbConfiguration
                {
                    Networking = "Enable",
                    VGpu = "Enable",
                    MemoryInMB = 8192,
                    LogonCommand = new WsbLogonCommand
                    {
                        Command = "powershell.exe -ExecutionPolicy Bypass -Command \"winget install -e --id Microsoft.VisualStudioCode --accept-package-agreements --accept-source-agreements; winget install -e --id Git.Git --accept-package-agreements --accept-source-agreements\""
                    }
                }
            },
            new()
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                Name = "Conferencing Test Bed",
                Description = "Camera and microphone passthrough with networking for safely testing conferencing applications.",
                Tags = new() { "Testing", "Conferencing" },
                IconGlyph = "\uE714", // Webcam icon
                Configuration = new WsbConfiguration
                {
                    Networking = "Enable",
                    VGpu = "Enable",
                    MemoryInMB = 4096,
                    AudioInput = "Enable",
                    VideoInput = "Enable",
                    ClipboardRedirection = "Enable"
                }
            },
            new()
            {
                Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
                Name = "Isolated Printer Bench",
                Description = "Offline sandbox with host printer redirection for testing print-dependent applications.",
                Tags = new() { "Testing", "Printing", "Offline" },
                IconGlyph = "\uE749", // Print icon
                Configuration = new WsbConfiguration
                {
                    Networking = "Disable",
                    VGpu = "Disable",
                    MemoryInMB = 4096,
                    ClipboardRedirection = "Disable",
                    PrinterRedirection = "Enable"
                }
            }
        };

        // Host mouse settings flow into the sandbox automatically on 24H2+; older
        // builds need a logon command to swap buttons for left-handed users. Any
        // non-Available status means the automatic behavior is absent.
        if (_capabilities.GetAvailability(SandboxFeature.HostMouseSettings).Status != FeatureStatus.Available)
        {
            presets.Add(new SandboxProfile
            {
                Id = Guid.Parse("66666666-6666-6666-6666-666666666666"),
                Name = "Left-Handed Desktop",
                Description = "Swaps the primary mouse button inside the sandbox for left-handed users (unnecessary on 24H2+, which follows host settings).",
                Tags = new() { "Accessibility" },
                IconGlyph = "\uE962", // Mouse icon
                Configuration = new WsbConfiguration
                {
                    Networking = "Enable",
                    VGpu = "Enable",
                    MemoryInMB = 4096,
                    LogonCommand = new WsbLogonCommand
                    {
                        Command = "cmd.exe /c rundll32.exe user32.dll,SwapMouseButton & reg add \"HKCU\\Control Panel\\Mouse\" /v SwapMouseButtons /t REG_SZ /d 1 /f"
                    }
                }
            });
        }

        return presets;
    }
}
