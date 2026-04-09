using System;
using System.Windows.Input;

namespace BugsFinder.ViewModels;

/// <summary>
/// A lightweight, framework-agnostic implementation of <see cref="ICommand"/> that
/// delegates <see cref="Execute"/> and <see cref="CanExecute"/> to caller-supplied
/// delegates. Demonstrates the Command pattern without any external MVVM library.
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    /// <inheritdoc/>
    public event EventHandler? CanExecuteChanged;

    /// <summary>
    /// Creates a <see cref="RelayCommand"/> with a parameterised execute action
    /// and an optional can-execute predicate.
    /// </summary>
    /// <param name="execute">Action invoked by <see cref="Execute"/>.</param>
    /// <param name="canExecute">Predicate that gates whether the command is available.</param>
    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute    = execute    ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <summary>
    /// Convenience constructor for commands that require no parameter.
    /// </summary>
    /// <param name="execute">Parameterless action.</param>
    /// <param name="canExecute">Optional parameterless gate predicate.</param>
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute is null ? null : _ => canExecute())
    {
        ArgumentNullException.ThrowIfNull(execute);
    }

    /// <inheritdoc/>
    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    /// <inheritdoc/>
    public void Execute(object? parameter) => _execute(parameter);

    /// <summary>
    /// Manually raises <see cref="CanExecuteChanged"/> so that WPF re-queries
    /// <see cref="CanExecute"/> and re-enables or disables bound controls.
    /// </summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
