namespace SandboxStudio.Core.Capabilities;

/// <summary>
/// A Windows Sandbox capability whose availability depends on the host OS build
/// or on administrator-configured Windows Sandbox policies.
/// </summary>
public enum SandboxFeature
{
    /// <summary>.wsb configuration files (Windows 10 build 18342+).</summary>
    ConfigurationFiles,

    /// <summary>Environment variables inside mapped folder host paths (Windows 11 23H2+).</summary>
    MappedFolderEnvironmentVariables,

    /// <summary>Host mouse settings applied to the sandbox automatically (Windows 11 24H2+).</summary>
    HostMouseSettings,

    /// <summary>The <c>wsb</c> command-line session-management tool (Windows 11 24H2+).</summary>
    WsbCli,

    /// <summary>Sandbox networking (<c>AllowNetworking</c> policy).</summary>
    Networking,

    /// <summary>Virtualized GPU sharing (<c>AllowVGPU</c> policy).</summary>
    VGpu,

    /// <summary>Microphone input (<c>AllowAudioInput</c> policy).</summary>
    AudioInput,

    /// <summary>Camera input (<c>AllowVideoInput</c> policy).</summary>
    VideoInput,

    /// <summary>Printer sharing (<c>AllowPrinterRedirection</c> policy).</summary>
    PrinterRedirection,

    /// <summary>Clipboard sharing (<c>AllowClipboardRedirection</c> policy).</summary>
    ClipboardRedirection,

    /// <summary>Host folder mapping (<c>AllowMappedFolders</c> policy, Windows 11 24H2+).</summary>
    MappedFolders,

    /// <summary>Writable host folder mappings (<c>AllowWriteToMappedFolders</c> policy, Windows 11 24H2+).</summary>
    WritableMappedFolders
}
