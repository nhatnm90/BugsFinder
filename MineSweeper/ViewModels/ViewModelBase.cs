using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MineSweeper.ViewModels;

/// <summary>
/// Abstract base class for all ViewModel types. Provides a hand-written implementation
/// of <see cref="INotifyPropertyChanged"/> — including the <see cref="SetProperty{T}"/>
/// helper — without relying on any external MVVM framework.
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raises <see cref="PropertyChanged"/> for the supplied property name.
    /// The compiler automatically fills in <paramref name="propertyName"/> when
    /// this method is called from inside a property setter.
    /// </summary>
    /// <param name="propertyName">Name of the property that changed (injected by the compiler).</param>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>
    /// Compares <paramref name="field"/> to <paramref name="value"/>; if they differ,
    /// updates the field and raises <see cref="PropertyChanged"/>.
    /// </summary>
    /// <typeparam name="T">Type of the backing field.</typeparam>
    /// <param name="field">Reference to the backing field.</param>
    /// <param name="value">New value to assign.</param>
    /// <param name="propertyName">Automatically provided by the compiler.</param>
    /// <returns><see langword="true"/> if the value changed; otherwise <see langword="false"/>.</returns>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
