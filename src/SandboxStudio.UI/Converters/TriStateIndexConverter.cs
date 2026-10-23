using Microsoft.UI.Xaml.Data;

namespace SandboxStudio.UI.Converters;

/// <summary>
/// Converts a Windows Sandbox tri-state string ("Default", "Enable", "Disable", or null)
/// to and from a ComboBox selected index (0 = Unspecified, 1 = Default, 2 = Enable, 3 = Disable).
/// </summary>
public sealed class TriStateIndexConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, string language) => value switch
    {
        "Default" => 1,
        "Enable" => 2,
        "Disable" => 3,
        null => 0,
        _ => -1
    };

    /// <inheritdoc/>
    public object? ConvertBack(object value, Type targetType, object parameter, string language) => value switch
    {
        1 => "Default",
        2 => "Enable",
        3 => "Disable",
        _ => null
    };
}
