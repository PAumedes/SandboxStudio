using SandboxStudio.Core.Models;
using SandboxStudio.Core.Validation;

namespace SandboxStudio.Core.Serialization;

/// <summary>
/// Outcome of importing a .wsb file into a stored profile.
/// </summary>
/// <param name="Profile">The profile created from the imported file.</param>
/// <param name="Validation">Validation result for the imported configuration.</param>
public sealed record ProfileImportResult(SandboxProfile Profile, ValidationResult Validation);
