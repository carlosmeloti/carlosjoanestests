using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace AcaiPos.Desktop.Behaviors;

public static class QuantityEditAssist
{
    public static readonly DependencyProperty CommitCommandProperty =
        DependencyProperty.RegisterAttached(
            "CommitCommand",
            typeof(ICommand),
            typeof(QuantityEditAssist),
            new PropertyMetadata(null, OnCommitCommandChanged));

    public static readonly DependencyProperty CancelCommandProperty =
        DependencyProperty.RegisterAttached(
            "CancelCommand",
            typeof(ICommand),
            typeof(QuantityEditAssist),
            new PropertyMetadata(null));

    public static ICommand? GetCommitCommand(DependencyObject element)
        => (ICommand?)element.GetValue(CommitCommandProperty);

    public static void SetCommitCommand(DependencyObject element, ICommand? value)
        => element.SetValue(CommitCommandProperty, value);

    public static ICommand? GetCancelCommand(DependencyObject element)
        => (ICommand?)element.GetValue(CancelCommandProperty);

    public static void SetCancelCommand(DependencyObject element, ICommand? value)
        => element.SetValue(CancelCommandProperty, value);

    private static void OnCommitCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox textBox)
            return;

        if (e.OldValue is null && e.NewValue is not null)
        {
            textBox.IsVisibleChanged += OnVisibleChanged;
            textBox.PreviewKeyDown += OnPreviewKeyDown;
            textBox.LostFocus += OnLostFocus;
        }
        else if (e.NewValue is null)
        {
            textBox.IsVisibleChanged -= OnVisibleChanged;
            textBox.PreviewKeyDown -= OnPreviewKeyDown;
            textBox.LostFocus -= OnLostFocus;
        }
    }

    private static void OnVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not TextBox textBox || !textBox.IsVisible)
            return;

        textBox.Dispatcher.BeginInvoke(() =>
        {
            if (!textBox.IsVisible)
                return;

            textBox.Focus();
            textBox.SelectAll();
        }, DispatcherPriority.Input);
    }

    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox)
            return;

        if (e.Key == Key.Enter)
        {
            Execute(GetCommitCommand(textBox), textBox.DataContext);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Execute(GetCancelCommand(textBox), textBox.DataContext);
            e.Handled = true;
        }
    }

    private static void OnLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox textBox || NumericKeypad.IsHolding(textBox))
            return;

        Execute(GetCommitCommand(textBox), textBox.DataContext);
    }

    private static void Execute(ICommand? command, object parameter)
    {
        if (command?.CanExecute(parameter) == true)
            command.Execute(parameter);
    }
}
