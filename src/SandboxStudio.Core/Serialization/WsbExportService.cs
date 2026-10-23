using SandboxStudio.Core.Models;
using SandboxStudio.Core.Validation;

namespace SandboxStudio.Core.Serialization;

/// <summary>
/// Validates Windows Sandbox configurations before exporting them to a .wsb file.
/// </summary>
public sealed class WsbExportService
{
    private readonly IWsbValidator _validator;
    private readonly IWsbSerializer _serializer;

    /// <summary>
    /// Initializes a new instance of <see cref="WsbExportService"/>.
    /// </summary>
    public WsbExportService(IWsbValidator validator, IWsbSerializer serializer)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    }

    /// <summary>
    /// Validates and exports a configuration to a Windows Sandbox file.
    /// </summary>
    public async Task<ValidationResult> ExportAsync(
        WsbConfiguration configuration,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var validationResult = _validator.Validate(configuration);
        if (!validationResult.IsValid)
        {
            return validationResult;
        }

        await _serializer.SerializeToFileAsync(configuration, filePath, cancellationToken);
        return validationResult;
    }
}
