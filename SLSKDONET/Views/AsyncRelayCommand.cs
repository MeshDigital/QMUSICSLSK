using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SLSKDONET.Views;

/// <summary>
/// An ICommand implementation that supports asynchronous operations.
/// </summary>
public class AsyncRelayCommand<T> : ICommand
{
    private readonly Func<T?, Task> _execute;
    private readonly Func<T?, bool>? _canExecute;
    private bool _isExecuting;

    public event EventHandler? CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }

    public AsyncRelayCommand(Func<T?, Task> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter)
    {
        return !_isExecuting && (_canExecute?.Invoke((T?)parameter) ?? true);
    }

    public async void Execute(object? parameter)
    {
        if (CanExecute(parameter))
        {
            try
            {
                _isExecuting = true;
                CommandManager.InvalidateRequerySuggested();
                await _execute((T?)parameter);
            }
            catch (Exception ex)
            {
                // Log the exception to prevent unobserved task exceptions
                System.Diagnostics.Debug.WriteLine($"AsyncRelayCommand exception: {ex.Message}");
                // Re-throw to let global handlers deal with it
                throw;
            }
            finally
            {
                _isExecuting = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }
}

/// <summary>
/// A generic version of AsyncRelayCommand for commands that accept a parameter.
/// </summary>
public class AsyncRelayCommand : AsyncRelayCommand<object>
{
    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
        : base(async _ => await execute(), _ => canExecute?.Invoke() ?? true)
    {
    }
}