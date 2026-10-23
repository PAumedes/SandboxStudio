using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace SandboxStudio.UI.Converters;

/// <summary>
/// Returns <see cref="Visibility.Visible"/> when the supplied path text is a fully qualified
/// path that does not exist on the host; otherwise <see cref="Visibility.Collapsed"/>.
/// </summary>
public sealed class MissingHostFolderVisibilityConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var path = value as string;
        var isMissing = !string.IsNullOrWhiteSpace(path) &&
            Path.IsPathFullyQualified(path) &&
            !Directory.Exists(path);

        return isMissing ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
