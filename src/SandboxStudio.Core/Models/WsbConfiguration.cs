using System.Xml.Serialization;

namespace SandboxStudio.Core.Models;

/// <summary>
/// Root schema model for a Windows Sandbox (.wsb) configuration file.
/// </summary>
[XmlRoot("Configuration")]
public sealed class WsbConfiguration
{
    /// <summary>
    /// Gets or sets whether virtualized GPU acceleration is enabled, disabled, or default.
    /// </summary>
    [XmlElement("VGpu")]
    public string? VGpu { get; set; }

    /// <summary>
    /// Gets or sets whether network access is enabled, disabled, or default.
    /// </summary>
    [XmlElement("Networking")]
    public string? Networking { get; set; }

    /// <summary>
    /// Gets or sets the memory allocation in megabytes.
    /// </summary>
    [XmlElement("MemoryInMB")]
    public int? MemoryInMB { get; set; }

    /// <summary>
    /// Controls whether the sandbox receives host audio/microphone input.
    /// </summary>
    [XmlElement("AudioInput")]
    public string? AudioInput { get; set; }

    /// <summary>
    /// Controls whether the sandbox receives host video/webcam input.
    /// </summary>
    [XmlElement("VideoInput")]
    public string? VideoInput { get; set; }

    /// <summary>
    /// Controls whether enhanced RDP client isolation mitigations are applied.
    /// </summary>
    [XmlElement("ProtectedClient")]
    public string? ProtectedClient { get; set; }

    /// <summary>
    /// Controls whether host-sandbox clipboard sharing is allowed.
    /// </summary>
    [XmlElement("ClipboardRedirection")]
    public string? ClipboardRedirection { get; set; }

    /// <summary>
    /// Controls whether host printers are redirected into the sandbox.
    /// </summary>
    [XmlElement("PrinterRedirection")]
    public string? PrinterRedirection { get; set; }

    /// <summary>
    /// Gets or sets the collection of mapped host folders.
    /// </summary>
    [XmlArray("MappedFolders")]
    [XmlArrayItem("MappedFolder")]
    public List<WsbMappedFolder> MappedFolders { get; set; } = new();

    /// <summary>
    /// Gets or sets the startup command executed on logon.
    /// </summary>
    [XmlElement("LogonCommand")]
    public WsbLogonCommand? LogonCommand { get; set; }

    /// <summary>
    /// Convenience property to determine whether MappedFolders should be serialized if empty.
    /// </summary>
    public bool ShouldSerializeMappedFolders() => MappedFolders.Count > 0;

    /// <summary>
    /// Convenience property to determine whether MemoryInMB should be serialized if null or invalid.
    /// </summary>
    public bool ShouldSerializeMemoryInMB() => MemoryInMB.HasValue && MemoryInMB.Value > 0;

    /// <summary>Determines whether VGpu should be serialized.</summary>
    public bool ShouldSerializeVGpu() => !string.IsNullOrWhiteSpace(VGpu);

    /// <summary>Determines whether Networking should be serialized.</summary>
    public bool ShouldSerializeNetworking() => !string.IsNullOrWhiteSpace(Networking);

    /// <summary>Determines whether AudioInput should be serialized.</summary>
    public bool ShouldSerializeAudioInput() => !string.IsNullOrWhiteSpace(AudioInput);

    /// <summary>Determines whether VideoInput should be serialized.</summary>
    public bool ShouldSerializeVideoInput() => !string.IsNullOrWhiteSpace(VideoInput);

    /// <summary>Determines whether ProtectedClient should be serialized.</summary>
    public bool ShouldSerializeProtectedClient() => !string.IsNullOrWhiteSpace(ProtectedClient);

    /// <summary>Determines whether ClipboardRedirection should be serialized.</summary>
    public bool ShouldSerializeClipboardRedirection() => !string.IsNullOrWhiteSpace(ClipboardRedirection);

    /// <summary>Determines whether PrinterRedirection should be serialized.</summary>
    public bool ShouldSerializePrinterRedirection() => !string.IsNullOrWhiteSpace(PrinterRedirection);

    /// <summary>Determines whether LogonCommand should be serialized.</summary>
    public bool ShouldSerializeLogonCommand() => LogonCommand is not null && !string.IsNullOrWhiteSpace(LogonCommand.Command);
}
