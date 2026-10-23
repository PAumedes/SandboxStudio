namespace SandboxStudio.Core.Models;

/// <summary>
/// Represents a user profile containing metadata and a Windows Sandbox configuration.
/// </summary>
public sealed class SandboxProfile
{
    /// <summary>
    /// Unique identifier for the profile.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Display name of the profile.
    /// </summary>
    public string Name { get; set; } = "New Sandbox Profile";

    /// <summary>
    /// Optional description explaining the profile's purpose.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Tags for filtering and categorization (e.g. #Security, #Development).
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Glyph or icon identifier for UI visualization.
    /// </summary>
    public string IconGlyph { get; set; } = "\uE7B8"; // Default: Settings / App icon

    /// <summary>
    /// Indicates whether the profile is marked as favorite for quick access.
    /// </summary>
    public bool IsFavorite { get; set; }

    /// <summary>
    /// Timestamp when this profile was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Timestamp when this profile was last launched.
    /// </summary>
    public DateTimeOffset? LastLaunchedAt { get; set; }

    /// <summary>
    /// The underlying Windows Sandbox XML configuration.
    /// </summary>
    public WsbConfiguration Configuration { get; set; } = new();

    /// <summary>
    /// Recipe for a packaged multi-line logon script (one command per entry), or
    /// <see langword="null"/> when the logon command is a plain single command.
    /// The packaged script and managed folder mapping live in <see cref="Configuration"/>;
    /// this list preserves the editable source for the profile editor.
    /// </summary>
    public List<string>? LogonScriptCommands { get; set; }
}
