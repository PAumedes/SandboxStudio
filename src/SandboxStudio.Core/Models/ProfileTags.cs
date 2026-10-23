namespace SandboxStudio.Core.Models;

/// <summary>
/// Parses, formats, and matches the free-form tags attached to profiles.
/// </summary>
public static class ProfileTags
{
    private static readonly char[] Separators = { ',', ';' };

    /// <summary>
    /// Splits raw tag input into normalized tags: entries are separated by commas or
    /// semicolons, trimmed, stripped of leading '#' characters, and deduplicated
    /// ignoring case (the first occurrence's casing wins). A null or empty input
    /// yields an empty list.
    /// </summary>
    public static List<string> Parse(string? input)
    {
        var tags = new List<string>();
        if (string.IsNullOrWhiteSpace(input))
        {
            return tags;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in input.Split(Separators))
        {
            var tag = entry.Trim().TrimStart('#').Trim();
            if (tag.Length > 0 && seen.Add(tag))
            {
                tags.Add(tag);
            }
        }

        return tags;
    }

    /// <summary>
    /// Formats tags as a comma-separated list suitable for editing or display.
    /// </summary>
    public static string Format(IEnumerable<string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);

        return string.Join(", ", tags);
    }

    /// <summary>
    /// Returns true when every required tag is present on the profile, ignoring case.
    /// An empty requirement list matches every profile.
    /// </summary>
    public static bool MatchesAll(IEnumerable<string> profileTags, IEnumerable<string> requiredTags)
    {
        ArgumentNullException.ThrowIfNull(profileTags);
        ArgumentNullException.ThrowIfNull(requiredTags);

        var ownedTags = new HashSet<string>(profileTags, StringComparer.OrdinalIgnoreCase);
        return requiredTags.All(ownedTags.Contains);
    }
}
