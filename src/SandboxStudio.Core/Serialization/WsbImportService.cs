using SandboxStudio.Core.Models;
using SandboxStudio.Core.Storage;
using SandboxStudio.Core.Validation;

namespace SandboxStudio.Core.Serialization;

/// <summary>
/// Imports existing Windows Sandbox (.wsb) files into stored profiles.
/// </summary>
public sealed class WsbImportService
{
    private readonly IWsbSerializer _serializer;
    private readonly IWsbValidator _validator;
    private readonly IProfileRepository _repository;

    /// <summary>
    /// Initializes a new instance of <see cref="WsbImportService"/>.
    /// </summary>
    public WsbImportService(IWsbSerializer serializer, IWsbValidator validator, IProfileRepository repository)
    {
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Reads a .wsb file, wraps it in a profile named after the file, validates the
    /// configuration, and saves the profile. The profile is stored even when validation
    /// reports errors so it can be corrected in the editor.
    /// </summary>
    /// <param name="filePath">Source .wsb file path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The imported profile and its validation outcome.</returns>
    public async Task<ProfileImportResult> ImportAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var configuration = await _serializer.DeserializeFromFileAsync(filePath, cancellationToken);

        var profile = new SandboxProfile
        {
            Name = Path.GetFileNameWithoutExtension(filePath),
            Description = $"Imported from {Path.GetFileName(filePath)}.",
            Configuration = configuration
        };

        var validation = _validator.Validate(configuration);
        await _repository.SaveAsync(profile, cancellationToken);
        return new ProfileImportResult(profile, validation);
    }
}
