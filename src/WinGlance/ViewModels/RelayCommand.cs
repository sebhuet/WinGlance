using System.Windows.Input;

namespace WinGlance.ViewModels;

/// <summary>
/// Lightweight <see cref="ICommand"/> implementation that delegates to an <see cref="Action{T}"/>.
/// CanExecuteChanged is wired to <see cref="CommandManager.RequerySuggested"/> for automatic UI refresh.
/// </summary>
public sealed class RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => execute(parameter);
}
