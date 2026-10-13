using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace AcaiPos.Desktop.ViewModels;

/// <summary>
/// Base das ViewModels. Notifica a interface quando uma propriedade muda.
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Atualiza o campo e dispara a notificação somente quando o valor mudou.
    /// </summary>
    protected void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Notifica a interface de que a propriedade informada mudou.
    /// </summary>
    protected void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>
/// Comando da interface que executa uma ação síncrona ou assíncrona e impede reentrância.
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Func<object?, Task>? _asyncExecute;
    private readonly Action<object?>? _execute;
    private readonly Func<object?, bool>? _canExecute;
    private bool _isExecuting;

    /// <summary>
    /// Cria um comando síncrono.
    /// </summary>
    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    /// <summary>
    /// Cria um comando assíncrono.
    /// </summary>
    public RelayCommand(Func<object?, Task> asyncExecute, Func<object?, bool>? canExecute = null)
    {
        _asyncExecute = asyncExecute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    /// <summary>
    /// Indica se o comando pode ser executado agora.
    /// </summary>
    public bool CanExecute(object? parameter)
        => !_isExecuting && (_canExecute?.Invoke(parameter) ?? true);

    /// <summary>
    /// Executa a ação e bloqueia novos cliques até o término.
    /// </summary>
    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
            return;

        try
        {
            _isExecuting = true;
            RaiseCanExecuteChanged();

            if (_asyncExecute is not null)
                await _asyncExecute(parameter);
            else
                _execute?.Invoke(parameter);
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// Avisa a interface para reavaliar se o comando pode ser executado.
    /// </summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
