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
    public QuickStartDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Gets a value indicating whether the guide should not appear on future launches.
    /// </summary>
    public bool DontShowAgain => DontShowAgainCheckBox.IsChecked == true;
}
