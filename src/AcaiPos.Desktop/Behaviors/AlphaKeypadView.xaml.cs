using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AcaiPos.Desktop.Behaviors;

public partial class AlphaKeypadView : UserControl
{
    private bool _uppercase;

    public bool IsUppercase => _uppercase;

    public AlphaKeypadView()
    {
        InitializeComponent();
        TopRow.SizeChanged += (_, _) => MatchKeyWidth();
    }

    public void SetUppercase(bool uppercase)
    {
        if (_uppercase == uppercase)
            return;

        _uppercase = uppercase;
        ApplyCase();
    }

    private void MatchKeyWidth()
    {
        var width = TopRow.ActualWidth * 9d / 10d;
        if (width <= 0)
            return;

        MiddleRow.Width = width;
        ShiftRow.Width = width;
    }

    private void ApplyCase()
    {
        foreach (var button in Descendants<Button>(this))
        {
            if (button.Tag is not string tag || tag.Length != 1 || !char.IsLetter(tag[0]))
                continue;

            button.Content = _uppercase
                ? char.ToUpperInvariant(tag[0]).ToString()
                : tag;
        }

        if (_uppercase)
            ShiftButton.Background = (Brush)FindResource("BrushAccent");
        else
            ShiftButton.ClearValue(Button.BackgroundProperty);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is not DependencyObject current)
                continue;

            if (current is T match)
                yield return match;

            foreach (var nested in Descendants<T>(current))
                yield return nested;
        }
    }
}
