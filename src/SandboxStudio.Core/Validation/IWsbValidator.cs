using SandboxStudio.Core.Models;

namespace SandboxStudio.Core.Validation;

/// <summary>
/// Service contract for validating Windows Sandbox configuration models.
/// </summary>
public interface IWsbValidator
{
    /// <summary>
    /// Validates a <see cref="WsbConfiguration"/> instance.
    /// </summary>
    /// <param name="configuration">The configuration to validate.</param>
    /// <returns>A <see cref="ValidationResult"/> detailing any errors or warnings.</returns>
    ValidationResult Validate(WsbConfiguration configuration);
}
