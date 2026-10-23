using System.Text;
using System.Xml;
using System.Xml.Serialization;
using SandboxStudio.Core.Models;

namespace SandboxStudio.Core.Serialization;

/// <summary>
/// High-performance serializer for Windows Sandbox (.wsb) configuration files.
/// </summary>
public sealed class WsbSerializer : IWsbSerializer
{
    private static readonly XmlSerializer Serializer = new(typeof(WsbConfiguration));
    private static readonly XmlSerializerNamespaces EmptyNamespaces = new(new[] { new XmlQualifiedName(string.Empty, string.Empty) });

    private static readonly XmlWriterSettings DefaultWriterSettings = new()
    {
        Indent = true,
        IndentChars = "  ",
        OmitXmlDeclaration = false,
        Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
    };

    private static readonly XmlReaderSettings DefaultReaderSettings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null
    };

    /// <inheritdoc/>
    public string Serialize(WsbConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        using var stringWriter = new Utf8StringWriter();
        using var xmlWriter = XmlWriter.Create(stringWriter, DefaultWriterSettings);

        Serializer.Serialize(xmlWriter, configuration, EmptyNamespaces);
        return stringWriter.ToString();
    }

    /// <inheritdoc/>
    public WsbConfiguration Deserialize(string xmlContent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xmlContent);

        using var stringReader = new StringReader(xmlContent);
        using var xmlReader = XmlReader.Create(stringReader, DefaultReaderSettings);

        var result = (WsbConfiguration?)Serializer.Deserialize(xmlReader);
        return result ?? throw new InvalidOperationException("Failed to deserialize the provided XML content into WsbConfiguration.");
    }

    /// <inheritdoc/>
    public async Task SerializeToFileAsync(WsbConfiguration configuration, string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var xml = Serialize(configuration);
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempFilePath = $"{filePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(tempFilePath, xml, new UTF8Encoding(false), cancellationToken);
            File.Move(tempFilePath, filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                try
                {
                    File.Delete(tempFilePath);
                }
                catch
                {
                    // Best-effort cleanup of the temporary file
                }
            }
        }
    }

    /// <inheritdoc/>
    public async Task<WsbConfiguration> DeserializeFromFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"WSB configuration file not found at: {filePath}", filePath);
        }

        var xml = await File.ReadAllTextAsync(filePath, cancellationToken);
        return Deserialize(xml);
    }

    private sealed class Utf8StringWriter : StringWriter
    {
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        public override Encoding Encoding => Utf8NoBom;
    }
}
