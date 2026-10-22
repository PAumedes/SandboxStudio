using System.Xml.Serialization;

namespace SandboxStudio.Core.Models;

/// <summary>
/// Represents a folder on the host machine shared with the sandbox.
/// </summary>
public sealed class WsbMappedFolder : IEquatable<WsbMappedFolder>
{
    /// <summary>
    /// Gets or sets the absolute path to the directory on the host computer.
    /// </summary>
    [XmlElement("HostFolder")]
    public string HostFolder { get; set; } = string.Empty;

    private string _readOnly = "true";

    /// <summary>
    /// Gets or sets the target destination path inside the sandbox desktop.
    /// If omitted, Windows Sandbox mounts it onto the sandbox user's desktop.
    /// </summary>
    [XmlElement("SandboxFolder")]
    public string? SandboxFolder { get; set; }

    /// <summary>
    /// Gets or sets whether the folder should be mounted in read-only mode ("true" or "false").
    /// Normalized to strictly lowercase "true" or "false" per Microsoft Windows Sandbox specification.
    /// </summary>
    [XmlElement("ReadOnly")]
    public string ReadOnly
    {
        get => _readOnly;
        set => _readOnly = string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) ? "false" : "true";
    }

    /// <summary>
    /// Convenient boolean wrapper for <see cref="ReadOnly"/>.
    /// </summary>
    [XmlIgnore]
    public bool IsReadOnly
    {
        get => string.Equals(ReadOnly, "true", StringComparison.OrdinalIgnoreCase);
        set => ReadOnly = value ? "true" : "false";
    }

    /// <summary>
    /// Determines whether SandboxFolder should be serialized if specified and non-empty.
    /// </summary>
    public bool ShouldSerializeSandboxFolder() => !string.IsNullOrWhiteSpace(SandboxFolder);

    /// <inheritdoc/>
    public bool Equals(WsbMappedFolder? other)
    {
        if (other is null)
        {
            return false;
        }

        return string.Equals(HostFolder, other.HostFolder, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(SandboxFolder, other.SandboxFolder, StringComparison.OrdinalIgnoreCase) &&
               IsReadOnly == other.IsReadOnly;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as WsbMappedFolder);

    /// <inheritdoc/>
    public override int GetHashCode() =>
        HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(HostFolder),
            SandboxFolder is null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(SandboxFolder),
            IsReadOnly);
}
