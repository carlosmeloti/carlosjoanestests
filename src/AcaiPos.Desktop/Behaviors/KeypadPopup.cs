using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace AcaiPos.Desktop.Behaviors;

/// <summary>
/// Popup flutuante compartilhado: posição, foco, inserção e confirmação do binding.
/// </summary>
internal sealed class KeypadPopup
{
    private static readonly List<KeypadPopup> All = [];

    private readonly Popup _popup;
    private readonly UserControl _view;
    private readonly Action<TextBox, string> _onKey;
    private readonly Dictionary<TextBox, Func<TextBox, bool>> _sameKind = new();
    private int? _lastTouchTimestamp;
    private bool _pressing;
    private bool _closing;

    public KeypadPopup(UserControl view, Action<TextBox, string> onKey)
    {
        _view = view;
        _onKey = onKey;
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

        All.Add(this);
        FloatingKeyboards.IsEnabledChanged += OnFloatingKeyboardsChanged;
    }

    public TextBox? Target { get; private set; }

    public bool IsPointerOver => _view.IsMouseOver || _view.IsKeyboardFocusWithin || _popup.IsMouseOver;

    public bool IsPressing => _pressing;

    public bool IsClosing => _closing;

    public bool IsHolding(TextBox textBox)
        => Target == textBox && (IsPointerOver || IsPressing);

    public void Hook(TextBox textBox, Action<TextBox> show, Func<TextBox, bool> isSameKind)
    {
        if (!_sameKind.TryAdd(textBox, isSameKind))
            return;

        textBox.GotKeyboardFocus += OnGotKeyboardFocus;
        textBox.PreviewLostKeyboardFocus += OnPreviewLostKeyboardFocus;
        textBox.LostKeyboardFocus += OnLostKeyboardFocus;
        textBox.IsVisibleChanged += OnIsVisibleChanged;
        textBox.Unloaded += OnUnloaded;
        textBox.SetValue(ShowCallbackProperty, show);
    }

    public void Unhook(TextBox textBox)
    {
        if (!_sameKind.Remove(textBox))
            return;

        textBox.GotKeyboardFocus -= OnGotKeyboardFocus;
        textBox.PreviewLostKeyboardFocus -= OnPreviewLostKeyboardFocus;
        textBox.LostKeyboardFocus -= OnLostKeyboardFocus;
        textBox.IsVisibleChanged -= OnIsVisibleChanged;
        textBox.Unloaded -= OnUnloaded;
        textBox.ClearValue(ShowCallbackProperty);
    }

    public void Show(TextBox textBox)
    {
        if (!FloatingKeyboards.IsEnabled || !textBox.IsVisible)
            return;

        foreach (var other in All.ToArray())
        {
            if (!ReferenceEquals(other, this) && other.Target is not null)
                other.CommitAndClose();
        }

        Target = textBox;
        _popup.PlacementTarget = textBox;
        if (_popup.IsOpen)
            _popup.IsOpen = false;

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

    public static void Insert(TextBox box, string text)
    {
        var current = box.Text ?? string.Empty;
        var start = Math.Clamp(box.SelectionStart, 0, current.Length);
        var length = Math.Clamp(box.SelectionLength, 0, current.Length - start);
        var nextLength = current.Length - length + text.Length;
        if (box.MaxLength > 0 && nextLength > box.MaxLength)
            return;

        var caret = start + text.Length;
        box.Text = current.Remove(start, length).Insert(start, text);
        PlaceCaret(box, caret);
    }

    public static void Backspace(TextBox box)
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

    private void OnFloatingKeyboardsChanged(object? sender, EventArgs e)
    {
        if (!FloatingKeyboards.IsEnabled && Target is not null)
            Close();
    }

    private void OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (!FloatingKeyboards.IsEnabled)
            return;

        if (sender is not TextBox textBox || !textBox.IsVisible)
            return;

        if (textBox.GetValue(ShowCallbackProperty) is Action<TextBox> show)
            show(textBox);
    }

    private void OnPreviewLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is not TextBox textBox || Target != textBox)
            return;

        if (IsClosing)
            return;

        if (IsPointerOver || IsPressing || e.NewFocus is null)
            e.Handled = true;
    }

    private void OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is not TextBox textBox || Target != textBox)
            return;

        if (e.NewFocus is TextBox next && _sameKind.TryGetValue(textBox, out var same) && same(next))
            return;

        textBox.Dispatcher.BeginInvoke(() =>
        {
            if (Target != textBox || textBox.IsKeyboardFocused || IsPointerOver || IsPressing)
                return;

            CommitAndClose();
        }, DispatcherPriority.Input);
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsClosing)
            return;

        if (sender is TextBox textBox && !textBox.IsVisible && Target == textBox)
            CommitAndClose();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && Target == textBox)
            Close();
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

        _onKey(box, button.Tag as string ?? string.Empty);
        if (IsClosing || Target != box)
            return;

        box.Focus();
        Keyboard.Focus(box);
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

    private static readonly DependencyProperty ShowCallbackProperty =
        DependencyProperty.RegisterAttached(
            "ShowCallback",
            typeof(Action<TextBox>),
            typeof(KeypadPopup),
            new PropertyMetadata(null));
}
