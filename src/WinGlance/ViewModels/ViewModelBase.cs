using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WinGlance.ViewModels;

/// <summary>
/// Base class for all ViewModels. Provides <see cref="INotifyPropertyChanged"/> support
/// with a <see cref="SetProperty{T}"/> helper for two-way data binding.
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Sets the backing field and raises <see cref="PropertyChanged"/> if the value changed.
    /// Returns true if the value was updated, false if it was already equal.
    /// </summary>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
