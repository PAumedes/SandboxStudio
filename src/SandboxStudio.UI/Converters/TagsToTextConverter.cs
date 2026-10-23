using Microsoft.UI.Xaml.Data;

namespace SandboxStudio.UI.Converters;

/// <summary>
/// Converts a profile's tag list into a display line such as "#Security #Dev";
/// empty or missing tags produce an empty string.
/// </summary>
public sealed class TagsToTextConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is IEnumerable<string> tags
            ? string.Join(" ", tags.Select(tag => $"#{tag}"))
            : string.Empty;

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
