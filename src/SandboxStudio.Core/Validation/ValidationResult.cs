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
/// <param name="PropertyName">Name of the property the message applies to.</param>
/// <param name="Message">Human-readable English fallback text.</param>
/// <param name="Severity">Severity of the issue.</param>
/// <param name="MessageKey">Optional <see cref="Localization.MessageKeys"/> entry identifying
/// the message template so hosts can localize it; <see cref="Message"/> remains the
/// English fallback.</param>
/// <param name="MessageArgs">Optional format arguments paired with <paramref name="MessageKey"/>.</param>
public sealed record ValidationMessage(
    string PropertyName,
    string Message,
    ValidationSeverity Severity,
    string? MessageKey = null,
    IReadOnlyList<object?>? MessageArgs = null
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
    /// <param name="propertyName">Name of the property the message applies to.</param>
    /// <param name="message">Human-readable English fallback text.</param>
    /// <param name="messageKey">Optional <see cref="Localization.MessageKeys"/> entry for host-side localization.</param>
    /// <param name="messageArgs">Optional format arguments paired with <paramref name="messageKey"/>.</param>
    public void AddError(string propertyName, string message, string? messageKey = null, IReadOnlyList<object?>? messageArgs = null) =>
        _messages.Add(new ValidationMessage(propertyName, message, ValidationSeverity.Error, messageKey, messageArgs));

    /// <summary>
    /// Adds a warning message.
    /// </summary>
    /// <param name="propertyName">Name of the property the message applies to.</param>
    /// <param name="message">Human-readable English fallback text.</param>
    /// <param name="messageKey">Optional <see cref="Localization.MessageKeys"/> entry for host-side localization.</param>
    /// <param name="messageArgs">Optional format arguments paired with <paramref name="messageKey"/>.</param>
    public void AddWarning(string propertyName, string message, string? messageKey = null, IReadOnlyList<object?>? messageArgs = null) =>
        _messages.Add(new ValidationMessage(propertyName, message, ValidationSeverity.Warning, messageKey, messageArgs));

    /// <summary>
    /// Adds an informational message.
    /// </summary>
    /// <param name="propertyName">Name of the property the message applies to.</param>
    /// <param name="message">Human-readable English fallback text.</param>
    /// <param name="messageKey">Optional <see cref="Localization.MessageKeys"/> entry for host-side localization.</param>
    /// <param name="messageArgs">Optional format arguments paired with <paramref name="messageKey"/>.</param>
    public void AddInfo(string propertyName, string message, string? messageKey = null, IReadOnlyList<object?>? messageArgs = null) =>
        _messages.Add(new ValidationMessage(propertyName, message, ValidationSeverity.Info, messageKey, messageArgs));
}
