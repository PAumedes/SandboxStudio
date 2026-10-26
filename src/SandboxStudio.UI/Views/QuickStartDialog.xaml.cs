using Microsoft.UI.Xaml.Controls;

namespace SandboxStudio.UI.Views;

/// <summary>
/// First-run quick-start guide; shown until the user checks "don't show again".
/// </summary>
public sealed partial class QuickStartDialog : ContentDialog
{
    /// <summary>
    /// Initializes a new instance of <see cref="QuickStartDialog"/>.
    /// </summary>
    /// <param name="dontShowAgain">Initial state of the "don't show again" box: the stored choice.</param>
    public QuickStartDialog(bool dontShowAgain)
    {
        InitializeComponent();
        DontShowAgainCheckBox.IsChecked = dontShowAgain;
    }

    /// <summary>
    /// Gets a value indicating whether the guide should not appear on future launches;
    /// unchecking the box when the guide is reopened brings it back.
    /// </summary>
    public bool DontShowAgain => DontShowAgainCheckBox.IsChecked == true;
}
