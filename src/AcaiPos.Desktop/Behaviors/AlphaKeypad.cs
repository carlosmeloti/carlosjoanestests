using System.Windows;
using System.Windows.Controls;

namespace AcaiPos.Desktop.Behaviors;

/// <summary>
/// Abre um único teclado alfabético abaixo do textbox que recebeu o foco.
/// </summary>
public static class AlphaKeypad
{
    private static KeypadPopup? _popup;
    private static AlphaKeypadView? _view;

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(AlphaKeypad),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static readonly DependencyProperty IsSuppressedProperty =
        DependencyProperty.RegisterAttached(
            "IsSuppressed",
            typeof(bool),
            typeof(AlphaKeypad),
            new PropertyMetadata(false, OnIsSuppressedChanged));

    public static bool GetIsEnabled(DependencyObject element)
        => (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value)
        => element.SetValue(IsEnabledProperty, value);

    public static bool GetIsSuppressed(DependencyObject element)
        => (bool)element.GetValue(IsSuppressedProperty);

    public static void SetIsSuppressed(DependencyObject element, bool value)
        => element.SetValue(IsSuppressedProperty, value);

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

    /// <summary>
    /// Fecha o teclado enquanto outra superfície, como a lista de sugestões, ocupa o espaço abaixo do campo.
    /// Ao liberar, reabre se o campo ainda estiver focado.
    /// </summary>
    private static void OnIsSuppressedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox textBox)
            return;

        if (e.NewValue is true)
        {
            var popup = Popup();
            if (popup.Target == textBox)
                popup.Close();
            return;
        }

        if (GetIsEnabled(textBox) && textBox.IsKeyboardFocused)
            Show(textBox);
    }

    private static void Show(TextBox textBox)
    {
        if (GetIsSuppressed(textBox))
            return;

        View().SetUppercase(false);
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
            case "shift":
                var view = View();
                view.SetUppercase(!view.IsUppercase);
                break;
            case "space":
                KeypadPopup.Insert(box, " ");
                break;
            case string letter when letter.Length == 1 && char.IsLetter(letter[0]):
                var text = View().IsUppercase
                    ? char.ToUpperInvariant(letter[0]).ToString()
                    : letter;
                KeypadPopup.Insert(box, text);
                break;
        }
    }

    private static KeypadPopup Popup()
    {
        if (_popup is not null)
            return _popup;

        _view = new AlphaKeypadView();
        _popup = new KeypadPopup(_view, OnKey);
        return _popup;
    }

    private static AlphaKeypadView View()
    {
        Popup();
        return _view!;
    }
}
