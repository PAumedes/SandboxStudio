using SandboxStudio.Core.Models;

namespace SandboxStudio.Core.Serialization;

/// <summary>
/// Service contract for serializing and deserializing Windows Sandbox (.wsb) configurations.
/// </summary>
public interface IWsbSerializer
{
    /// <summary>
    /// Serializes a <see cref="WsbConfiguration"/> into clean .wsb-compliant XML string.
    /// </summary>
    /// <param name="configuration">The configuration instance.</param>
    /// <returns>Formatted XML string.</returns>
    string Serialize(WsbConfiguration configuration);

    /// <summary>
    /// Deserializes a .wsb XML string into a <see cref="WsbConfiguration"/> model.
    /// </summary>
    /// <param name="xmlContent">The XML string.</param>
    /// <returns>The deserialized configuration model.</returns>
    WsbConfiguration Deserialize(string xmlContent);

    /// <summary>
    /// Serializes and writes a .wsb configuration directly to a target file path.
    /// </summary>
    /// <param name="configuration">The configuration instance.</param>
    /// <param name="filePath">Target destination file path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SerializeToFileAsync(WsbConfiguration configuration, string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads and deserializes a .wsb configuration file.
    /// </summary>
    /// <param name="filePath">Source file path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deserialized configuration model.</returns>
    Task<WsbConfiguration> DeserializeFromFileAsync(string filePath, CancellationToken cancellationToken = default);
}
