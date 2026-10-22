using System.Xml.Serialization;

namespace SandboxStudio.Core.Models;

/// <summary>
/// Represents a command or script executed automatically when the sandbox boots up.
/// </summary>
public sealed class WsbLogonCommand : IEquatable<WsbLogonCommand>
{
    /// <summary>
    /// Gets or sets the command-line string to execute inside the sandbox upon logon.
    /// </summary>
    [XmlElement("Command")]
    public string Command { get; set; } = string.Empty;

    /// <inheritdoc/>
    public bool Equals(WsbLogonCommand? other) =>
        other is not null && string.Equals(Command, other.Command, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as WsbLogonCommand);

    /// <inheritdoc/>
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Command);
}
