using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace AcaiPos.Desktop.Behaviors;

/// <summary>
/// Destaque e toque na lista de sugestões da venda. O arrasto rola; um toque curto lança o item.
/// </summary>
public static class SuggestionPick
{
    private const double TapSlop = 16;
    private static int _lastTouchTimestamp;
    private static int _lastAcceptTimestamp;

    public static readonly DependencyProperty AcceptCommandProperty =
        DependencyProperty.RegisterAttached(
            "AcceptCommand",
            typeof(ICommand),
            typeof(SuggestionPick),
            new PropertyMetadata(null, OnAcceptCommandChanged));

    public static readonly DependencyProperty HighlightedIndexProperty =
        DependencyProperty.RegisterAttached(
            "HighlightedIndex",
            typeof(int),
            typeof(SuggestionPick),
            new PropertyMetadata(-1, OnHighlightedIndexChanged));

    private static readonly DependencyProperty OriginProperty =
        DependencyProperty.RegisterAttached(
            "Origin",
            typeof(Point),
            typeof(SuggestionPick));

    private static readonly DependencyProperty TrackingProperty =
        DependencyProperty.RegisterAttached(
            "Tracking",
            typeof(bool),
            typeof(SuggestionPick),
            new PropertyMetadata(false));

    private static readonly DependencyProperty ItemsHandlerProperty =
        DependencyProperty.RegisterAttached(
            "ItemsHandler",
            typeof(NotifyCollectionChangedEventHandler),
            typeof(SuggestionPick));

    public static ICommand? GetAcceptCommand(DependencyObject element)
        => (ICommand?)element.GetValue(AcceptCommandProperty);

    public static void SetAcceptCommand(DependencyObject element, ICommand? value)
        => element.SetValue(AcceptCommandProperty, value);

    public static int GetHighlightedIndex(DependencyObject element)
        => (int)element.GetValue(HighlightedIndexProperty);

    public static void SetHighlightedIndex(DependencyObject element, int value)
        => element.SetValue(HighlightedIndexProperty, value);

    private static void OnAcceptCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ListBox list)
            return;

        if (e.OldValue is null && e.NewValue is not null)
            Hook(list);
        else if (e.OldValue is not null && e.NewValue is null)
            Unhook(list);
    }

    private static void Hook(ListBox list)
    {
        list.PreviewMouseLeftButtonDown += OnMouseDown;
        list.PreviewMouseLeftButtonUp += OnMouseUp;
        list.PreviewTouchDown += OnTouchDown;
        list.PreviewTouchUp += OnTouchUp;

        NotifyCollectionChangedEventHandler handler = (_, _) =>
            list.Dispatcher.BeginInvoke(
                () => ApplyHighlight(list, GetHighlightedIndex(list)),
                DispatcherPriority.Loaded);
        list.SetValue(ItemsHandlerProperty, handler);
        ((INotifyCollectionChanged)list.Items).CollectionChanged += handler;
    }

    private static void Unhook(ListBox list)
    {
        list.PreviewMouseLeftButtonDown -= OnMouseDown;
        list.PreviewMouseLeftButtonUp -= OnMouseUp;
        list.PreviewTouchDown -= OnTouchDown;
        list.PreviewTouchUp -= OnTouchUp;

        if (list.GetValue(ItemsHandlerProperty) is NotifyCollectionChangedEventHandler handler)
        {
            ((INotifyCollectionChanged)list.Items).CollectionChanged -= handler;
            list.ClearValue(ItemsHandlerProperty);
        }
    }

    private static void OnHighlightedIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ListBox list)
            ApplyHighlight(list, (int)e.NewValue);
    }

    /// <summary>
    /// O clique no item não pode gravar SelectedIndex: isso removeria o vínculo com o destaque do teclado.
    /// A rolagem do toque continua pelo evento de toque, que não é marcado como tratado.
    /// </summary>
    private static void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox list || !IsItemSource(e.OriginalSource as DependencyObject))
            return;

        if (e.Timestamp - _lastTouchTimestamp is >= 0 and < 200)
        {
            e.Handled = true;
            return;
        }

        list.SetValue(TrackingProperty, true);
        list.SetValue(OriginProperty, e.GetPosition(list));
        e.Handled = true;
    }

    private static void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox list || list.GetValue(TrackingProperty) is not true)
            return;

        if (e.Timestamp - _lastTouchTimestamp is >= 0 and < 200)
            return;

        list.SetValue(TrackingProperty, false);
        TryAccept(list, e.OriginalSource as DependencyObject, e.GetPosition(list), e.Timestamp);
        e.Handled = true;
    }

    private static void OnTouchDown(object? sender, TouchEventArgs e)
    {
        if (sender is not ListBox list || !IsItemSource(e.OriginalSource as DependencyObject))
            return;

        _lastTouchTimestamp = e.Timestamp;
        list.SetValue(TrackingProperty, true);
        list.SetValue(OriginProperty, e.GetTouchPoint(list).Position);
    }

    private static void OnTouchUp(object? sender, TouchEventArgs e)
    {
        if (sender is not ListBox list || list.GetValue(TrackingProperty) is not true)
            return;

        list.SetValue(TrackingProperty, false);
        TryAccept(list, e.OriginalSource as DependencyObject, e.GetTouchPoint(list).Position, e.Timestamp);
    }

    private static bool IsItemSource(DependencyObject? source)
        => FindAncestor<ScrollBar>(source) is null && FindAncestor<ListBoxItem>(source) is not null;

    private static void TryAccept(ListBox list, DependencyObject? source, Point current, int timestamp)
    {
        if (timestamp - _lastAcceptTimestamp is >= 0 and < 300)
            return;

        var origin = (Point)list.GetValue(OriginProperty);
        var dx = current.X - origin.X;
        var dy = current.Y - origin.Y;
        if (dx * dx + dy * dy > TapSlop * TapSlop)
            return;

        var item = FindAncestor<ListBoxItem>(source);
        if (item?.Content is null)
            return;

        var command = GetAcceptCommand(list);
        if (command?.CanExecute(item.Content) != true)
            return;

        _lastAcceptTimestamp = timestamp;
        command.Execute(item.Content);
    }

    private static void ApplyHighlight(ListBox list, int index)
    {
        if (index >= list.Items.Count)
            index = -1;

        if (list.SelectedIndex != index)
            list.SelectedIndex = index;

        if (index >= 0)
            list.ScrollIntoView(list.Items[index]);
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
                return match;

            if (current is not Visual visual)
                return null;

            current = VisualTreeHelper.GetParent(visual);
        }

        return null;
    }
}
