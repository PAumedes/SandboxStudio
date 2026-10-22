using SandboxStudio.Core.Models;

namespace SandboxStudio.Core.Storage;

/// <summary>
/// Contract for managing user profile persistence and retrieval.
/// </summary>
public interface IProfileRepository
{
    /// <summary>
    /// Retrieves all saved profiles.
    /// </summary>
    Task<IReadOnlyList<SandboxProfile>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single profile by its unique identifier.
    /// </summary>
    Task<SandboxProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists or updates a profile.
    /// </summary>
    Task SaveAsync(SandboxProfile profile, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a profile by its unique identifier.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures the built-in presets have been initialized for a new profile store.
    /// </summary>
    Task EnsureDefaultPresetsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates built-in curated preset profiles.
    /// </summary>
    IReadOnlyList<SandboxProfile> GetDefaultPresets();
}
