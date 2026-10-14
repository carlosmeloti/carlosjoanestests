using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace AcaiPos.Desktop.Behaviors;

/// <summary>
/// Abre um único teclado numérico abaixo do textbox que recebeu o foco.
/// </summary>
public static class NumericKeypad
{
    private static KeypadHost? _host;

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

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox textBox)
            return;

        if (e.NewValue is true)
        {
            textBox.GotKeyboardFocus += OnGotKeyboardFocus;
            textBox.PreviewLostKeyboardFocus += OnPreviewLostKeyboardFocus;
            textBox.LostKeyboardFocus += OnLostKeyboardFocus;
            textBox.IsVisibleChanged += OnIsVisibleChanged;
            textBox.Unloaded += OnUnloaded;
        }
        else
        {
            textBox.GotKeyboardFocus -= OnGotKeyboardFocus;
            textBox.PreviewLostKeyboardFocus -= OnPreviewLostKeyboardFocus;
            textBox.LostKeyboardFocus -= OnLostKeyboardFocus;
            textBox.IsVisibleChanged -= OnIsVisibleChanged;
            textBox.Unloaded -= OnUnloaded;
            if (_host?.Target == textBox)
                _host.Close();
        }
    }

    private static void OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is not TextBox textBox || !textBox.IsVisible)
            return;

        Host().Show(textBox);
    }

    private static void OnPreviewLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is not TextBox textBox || _host?.Target != textBox)
            return;

        if (_host.IsClosing)
            return;

        if (_host.IsPointerOver || _host.IsPressing || e.NewFocus is null)
            e.Handled = true;
    }

    private static void OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is not TextBox textBox || _host?.Target != textBox)
            return;

        if (e.NewFocus is TextBox next && GetIsEnabled(next))
            return;

        textBox.Dispatcher.BeginInvoke(() =>
        {
            if (_host?.Target != textBox || textBox.IsKeyboardFocused || _host.IsPointerOver || _host.IsPressing)
                return;

            _host.CommitAndClose();
        }, DispatcherPriority.Input);
    }

    private static void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_host is not KeypadHost host || host.IsClosing)
            return;

        if (sender is TextBox textBox && !textBox.IsVisible && host.Target == textBox)
            host.CommitAndClose();
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && _host?.Target == textBox)
            _host.Close();
    }

    /// <summary>
    /// Indica que o toque atual é no teclado deste campo, para o foco não confirmar o valor no meio da digitação.
    /// </summary>
    public static bool IsHolding(TextBox textBox)
        => _host?.Target == textBox && (_host.IsPointerOver || _host.IsPressing);

    private static KeypadHost Host() => _host ??= new KeypadHost();

    private sealed class KeypadHost
    {
        private readonly Popup _popup;
        private readonly NumericKeypadView _view;
        private int? _lastTouchTimestamp;
        private bool _pressing;
        private bool _closing;

        public KeypadHost()
        {
            _view = new NumericKeypadView();
            _view.PreviewMouseDown += OnPreviewMouseDown;
            _view.PreviewTouchDown += OnPreviewTouchDown;

            _popup = new Popup
            {
                AllowsTransparency = true,
                StaysOpen = true,
                Focusable = false,
                Placement = PlacementMode.Custom,
                PlacementTarget = null,
                Child = _view,
                CustomPopupPlacementCallback = Place
            };
        }

        public TextBox? Target { get; private set; }

        public bool IsPointerOver => _view.IsMouseOver || _view.IsKeyboardFocusWithin || _popup.IsMouseOver;

        public bool IsPressing => _pressing;

        public bool IsClosing => _closing;

        public void Show(TextBox textBox)
        {
            Target = textBox;
            _view.SetDecimalEnabled(GetAllowDecimal(textBox));
            _popup.PlacementTarget = textBox;
            if (_popup.IsOpen)
            {
                _popup.IsOpen = false;
            }

            _popup.IsOpen = true;
        }

        public void CommitAndClose()
        {
            var box = Target;
            _closing = true;
            try
            {
                if (box is not null)
                    box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();

                Close();
                if (box is null)
                    return;

                var commit = QuantityEditAssist.GetCommitCommand(box);
                if (commit?.CanExecute(box.DataContext) == true)
                    commit.Execute(box.DataContext);

                if (box.IsKeyboardFocused)
                    Keyboard.ClearFocus();
            }
            finally
            {
                _closing = false;
            }
        }

        public void Close()
        {
            _popup.IsOpen = false;
            Target = null;
        }

        private void OnPreviewTouchDown(object? sender, TouchEventArgs e)
        {
            _lastTouchTimestamp = e.Timestamp;
            HandlePress(e.OriginalSource as DependencyObject, e);
        }

        private void OnPreviewMouseDown(object? sender, MouseButtonEventArgs e)
        {
            if (_lastTouchTimestamp is int touch && e.Timestamp - touch is >= 0 and < 200)
            {
                e.Handled = true;
                return;
            }

            HandlePress(e.OriginalSource as DependencyObject, e);
        }

        private void HandlePress(DependencyObject? source, InputEventArgs e)
        {
            e.Handled = true;
            _pressing = true;
            _view.Dispatcher.BeginInvoke(() => _pressing = false, DispatcherPriority.Background);
            var box = Target;
            if (box is null)
                return;

            var button = FindAncestor<Button>(source);
            if (button is null)
            {
                box.Focus();
                Keyboard.Focus(box);
                return;
            }

            switch (button.Tag as string)
            {
                case "close":
                    CommitAndClose();
                    return;
                case "back":
                    Backspace(box);
                    break;
                case ",":
                    if (GetAllowDecimal(box))
                        Insert(box, ",");
                    break;
                case string digit:
                    Insert(box, digit);
                    break;
            }

            box.Focus();
            Keyboard.Focus(box);
        }

        private static void Insert(TextBox box, string text)
        {
            var current = box.Text ?? string.Empty;
            var start = Math.Clamp(box.SelectionStart, 0, current.Length);
            var length = Math.Clamp(box.SelectionLength, 0, current.Length - start);
            if (text == "," && (current.Contains(',') || current.Contains('.')))
                return;

            var nextLength = current.Length - length + text.Length;
            if (box.MaxLength > 0 && nextLength > box.MaxLength)
                return;

            var caret = start + text.Length;
            box.Text = current.Remove(start, length).Insert(start, text);
            PlaceCaret(box, caret);
        }

        private static void Backspace(TextBox box)
        {
            var current = box.Text ?? string.Empty;
            var start = Math.Clamp(box.SelectionStart, 0, current.Length);
            var length = Math.Clamp(box.SelectionLength, 0, current.Length - start);
            if (length > 0)
            {
                box.Text = current.Remove(start, length);
                PlaceCaret(box, start);
                return;
            }

            if (start == 0)
                return;

            box.Text = current.Remove(start - 1, 1);
            PlaceCaret(box, start - 1);
        }

        private static void PlaceCaret(TextBox box, int caret)
        {
            box.Dispatcher.BeginInvoke(() =>
            {
                var length = box.Text?.Length ?? 0;
                box.SelectionStart = Math.Clamp(caret, 0, length);
                box.SelectionLength = 0;
            }, DispatcherPriority.Background);
        }

        private CustomPopupPlacement[] Place(Size popupSize, Size targetSize, Point offset)
        {
            const double gap = 8;
            var belowY = targetSize.Height + gap;
            var aboveY = -popupSize.Height - gap;
            var x = 0d;
            var y = belowY;

            if (_popup.PlacementTarget is FrameworkElement target)
            {
                var source = PresentationSource.FromVisual(target);
                if (source?.CompositionTarget is not null)
                {
                    var topLeft = source.CompositionTarget.TransformFromDevice.Transform(target.PointToScreen(new Point(0, 0)));
                    var work = SystemParameters.WorkArea;
                    if (topLeft.Y + targetSize.Height + gap + popupSize.Height > work.Bottom)
                        y = aboveY;

                    var left = topLeft.X;
                    var right = left + popupSize.Width;
                    if (right > work.Right)
                        x = work.Right - right;
                    if (left + x < work.Left)
                        x = work.Left - left;
                }
            }

            return [new CustomPopupPlacement(new Point(x, y), PopupPrimaryAxis.Horizontal)];
        }

        private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
        {
            while (current is not null)
            {
                if (current is T match)
                    return match;

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }
    }
}
