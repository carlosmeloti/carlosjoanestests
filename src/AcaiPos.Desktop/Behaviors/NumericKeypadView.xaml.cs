using System.Windows.Controls;

namespace AcaiPos.Desktop.Behaviors;

public partial class NumericKeypadView : UserControl
{
    public NumericKeypadView()
    {
        InitializeComponent();
    }

    public void SetDecimalEnabled(bool enabled) => CommaButton.IsEnabled = enabled;
}
