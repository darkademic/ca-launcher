using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CALauncher.ViewModels;

public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => _execute();

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private readonly Func<bool>? _allowExecutionDuringAsync;
    private bool _isExecuting;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null, Func<bool>? allowExecutionDuringAsync = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
        _allowExecutionDuringAsync = allowExecutionDuringAsync;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        if (_isExecuting && _allowExecutionDuringAsync?.Invoke() == true)
            return true;

        return !_isExecuting && (_canExecute?.Invoke() ?? true);
    }

    public async void Execute(object? parameter)
    {
        if (CanExecute(parameter))
        {
            try
            {
                _isExecuting = true;
                RaiseCanExecuteChanged();
                await _execute();
            }
            finally
            {
                _isExecuting = false;
                RaiseCanExecuteChanged();
            }
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
