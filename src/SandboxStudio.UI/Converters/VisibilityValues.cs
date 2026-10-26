using Microsoft.UI.Xaml;

namespace SandboxStudio.UI.Converters;

/// <summary>
/// Static helpers for <c>x:Bind</c> calls that need a <see cref="Visibility"/>:
/// <c>x:Bind</c> has no implicit bool-to-visibility conversion, so templates call
/// these functions (the same pattern <see cref="ProfileCardText"/> uses).
/// </summary>
public static class VisibilityValues
{
    /// <summary>Returns <see cref="Visibility.Visible"/> when <paramref name="value"/> is true.</summary>
    public static Visibility ToVisibility(bool value) =>
        value ? Visibility.Visible : Visibility.Collapsed;
}
