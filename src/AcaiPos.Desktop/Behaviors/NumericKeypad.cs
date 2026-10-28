using System.Windows;
using System.Windows.Controls;

namespace AcaiPos.Desktop.Behaviors;

/// <summary>
/// Abre um único teclado numérico abaixo do textbox que recebeu o foco.
/// </summary>
public static class NumericKeypad
{
    private static KeypadPopup? _popup;
    private static NumericKeypadView? _view;

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(NumericKeypad),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static readonly DependencyProperty AllowDecimalProperty =
        DependencyProperty.RegisterAttached(
            "AllowDecimal",
            typeof(bool),
            typeof(NumericKeypad),
            new PropertyMetadata(true));

    public static bool GetIsEnabled(DependencyObject element)
        => (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value)
        => element.SetValue(IsEnabledProperty, value);

    public static bool GetAllowDecimal(DependencyObject element)
        => (bool)element.GetValue(AllowDecimalProperty);

    public static void SetAllowDecimal(DependencyObject element, bool value)
        => element.SetValue(AllowDecimalProperty, value);

    /// <summary>
    /// Indica que o toque atual é no teclado deste campo, para o foco não confirmar o valor no meio da digitação.
    /// </summary>
    public static bool IsHolding(TextBox textBox)
        => _popup?.IsHolding(textBox) == true;

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox textBox)
            return;

        var popup = Popup();
        if (e.NewValue is true)
        {
            popup.Hook(textBox, Show, GetIsEnabled);
        }
        else
        {
            popup.Unhook(textBox);
            if (popup.Target == textBox)
                popup.Close();
        }
    }

    private static void Show(TextBox textBox)
    {
        View().SetDecimalEnabled(GetAllowDecimal(textBox));
        Popup().Show(textBox);
    }

    private static void OnKey(TextBox box, string tag)
    {
        switch (tag)
        {
            case "close":
                Popup().CommitAndClose();
                return;
            case "back":
                KeypadPopup.Backspace(box);
                break;
            case ",":
                if (!GetAllowDecimal(box))
                    break;

                var current = box.Text ?? string.Empty;
                if (current.Contains(',') || current.Contains('.'))
                    break;

                KeypadPopup.Insert(box, ",");
                break;
            case string digit:
                KeypadPopup.Insert(box, digit);
                break;
        }
    }

    private static KeypadPopup Popup()
    {
        if (_popup is not null)
            return _popup;

        _view = new NumericKeypadView();
        _popup = new KeypadPopup(_view, OnKey);
        return _popup;
    }

    private static NumericKeypadView View()
    {
        Popup();
        return _view!;
    }
}
