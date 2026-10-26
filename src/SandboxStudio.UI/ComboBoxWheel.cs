using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace SandboxStudio.UI;

/// <summary>
/// A closed ComboBox consumes the mouse wheel to cycle its selection. That
/// silently changes settings while the user tries to scroll, so dialogs route
/// the wheel through <see cref="Redirect"/> to the surrounding ScrollViewer.
/// </summary>
internal static class ComboBoxWheel
{
    /// <summary>
    /// <c>PointerWheelChanged</c> handler: stops a closed ComboBox from cycling
    /// its selection and scrolls the nearest ancestor ScrollViewer instead.
    /// </summary>
    public static void Redirect(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not ComboBox combo || combo.IsDropDownOpen)
        {
            return;
        }

        e.Handled = true;
        var properties = e.GetCurrentPoint(null).Properties;
        if (properties.IsHorizontalMouseWheel)
        {
            // Swallowed only: a sideways tilt must not scroll the page vertically.
            return;
        }

        // One wheel detent reports ~120 units; ScrollViewer's native wheel step
        // is ~half that, so halving keeps scrolling over a combo consistent
        // with scrolling over plain content.
        var delta = properties.MouseWheelDelta / 2.0;
        for (DependencyObject? element = combo; element is not null; element = VisualTreeHelper.GetParent(element))
        {
            if (element is ScrollViewer scroller)
            {
                scroller.ChangeView(null, scroller.VerticalOffset - delta, null, disableAnimation: true);
                return;
            }
        }
    }
}
