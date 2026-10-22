namespace SandboxStudio.Core.Validation;

/// <summary>
/// Severity of a validation message.
/// </summary>
public enum ValidationSeverity
{
    /// <summary>Information or recommendation.</summary>
    Info,

    /// <summary>Potential issue that may cause unexpected behavior.</summary>
    Warning,

    /// <summary>Definitive error preventing successful sandbox execution.</summary>
    Error
}

/// <summary>
/// Represents an individual issue discovered during configuration validation.
/// </summary>
public sealed record ValidationMessage(
    string PropertyName,
    string Message,
    ValidationSeverity Severity
);

/// <summary>
/// Represents the aggregate outcome of validating a sandbox configuration.
/// </summary>
public sealed class ValidationResult
{
    private readonly List<ValidationMessage> _messages = new();

    /// <summary>
    /// Gets all validation messages.
    /// </summary>
    public IReadOnlyList<ValidationMessage> Messages => _messages;

    /// <summary>
    /// Indicates whether the configuration is valid (contains zero errors).
    /// </summary>
    public bool IsValid => _messages.All(m => m.Severity != ValidationSeverity.Error);

    /// <summary>
    /// Adds an error message.
    /// </summary>
    public void AddError(string propertyName, string message) =>
        _messages.Add(new ValidationMessage(propertyName, message, ValidationSeverity.Error));

    /// <summary>
    /// Adds a warning message.
    /// </summary>
    public void AddWarning(string propertyName, string message) =>
        _messages.Add(new ValidationMessage(propertyName, message, ValidationSeverity.Warning));

    /// <summary>
    /// Adds an informational message.
    /// </summary>
    public void AddInfo(string propertyName, string message) =>
        _messages.Add(new ValidationMessage(propertyName, message, ValidationSeverity.Info));
}
