using System.Text.RegularExpressions;
using SandboxStudio.Core.Models;

namespace SandboxStudio.Core.Storage;

/// <summary>
/// Duplicates saved profiles under unique "(copy)"-suffixed names.
/// </summary>
public sealed class ProfileDuplicationService
{
    private static readonly Regex CopySuffixPattern = new(
        @" \(copy( \d+)?\)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly IProfileRepository _repository;

    /// <summary>
    /// Initializes a new instance of <see cref="ProfileDuplicationService"/>.
    /// </summary>
    public ProfileDuplicationService(IProfileRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Creates a deep copy of the profile with a fresh identity and a unique name,
    /// then persists it.
    /// </summary>
    /// <param name="sourceProfileId">Identifier of the profile to copy.</param>
    /// <param name="cancellationToken">Token cancelling the repository operations.</param>
    /// <returns>The persisted duplicate, or null when the source profile does not exist.</returns>
    public async Task<SandboxProfile?> DuplicateAsync(Guid sourceProfileId, CancellationToken cancellationToken = default)
    {
        var source = await _repository.GetByIdAsync(sourceProfileId, cancellationToken);
        if (source is null)
        {
            return null;
        }

        if (source.Name is null || source.Configuration is null || source.Tags is null)
        {
            throw new InvalidOperationException(
                $"Profile '{sourceProfileId}' cannot be duplicated because its stored data is incomplete.");
        }

        var existingNames = (await _repository.GetAllAsync(cancellationToken))
            .Select(profile => profile.Name)
            .ToList();

        var duplicate = SandboxProfileCopy.Duplicate(source);
        duplicate.Name = GetUniqueCopyName(source.Name, existingNames);

        await _repository.SaveAsync(duplicate, cancellationToken);
        return duplicate;
    }

    /// <summary>
    /// Appends " (copy)" or " (copy N)" to the base name, reusing the lowest free
    /// suffix like Windows Explorer. A source name already ending in a copy suffix
    /// contributes its base name so numbering continues instead of nesting.
    /// </summary>
    private static string GetUniqueCopyName(string sourceName, IReadOnlyCollection<string> existingNames)
    {
        var baseName = CopySuffixPattern.Replace(sourceName, string.Empty);
        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = sourceName;
        }

        for (var suffix = 1; ; suffix++)
        {
            var candidate = suffix == 1
                ? $"{baseName} (copy)"
                : $"{baseName} (copy {suffix})";
            if (!existingNames.Contains(candidate, StringComparer.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }
    }
}
