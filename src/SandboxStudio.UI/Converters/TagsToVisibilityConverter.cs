using System.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace SandboxStudio.UI.Converters;

/// <summary>
/// Returns <see cref="Visibility.Visible"/> when the supplied tag collection contains
/// at least one tag; otherwise <see cref="Visibility.Collapsed"/>.
/// </summary>
public sealed class TagsToVisibilityConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is ICollection { Count: > 0 } ? Visibility.Visible : Visibility.Collapsed;

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
