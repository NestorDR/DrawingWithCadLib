using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DrawingWithCadLib;

public class BaseClass : INotifyPropertyChanged
{
    private bool _hasChanges;
    public virtual bool HasChanges
    {
        get => _hasChanges;
        set => SetProperty(ref _hasChanges, value);
    }

    #region INotifyPropertyChanged

    /// <summary>
    /// The PropertyChanged Event to raise to any UI object
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Notifies to the listeners that a property value has changed, using PropertyChanged Event to raise to any UI object.
    /// [CallerMemberName] allows to get the name of the calling property avoiding writing it explicitly.
    /// This method is often also named: RaisePropertyChange or OnPropertyChanged
    /// </summary>
    /// <param name="propertyName">The property name that is changing and is used for notifying listeners.</param>
    protected virtual void NotifyPropertyChanged([CallerMemberName] string? propertyName = null) =>
        // Raise the PropertyChanged event, if handler is connected
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>
    /// Sets property if it does not equal existing value. Notifies listeners whether or not a change occurs.
    /// </summary>
    /// <param name="member">The property's backing field.</param>
    /// <param name="value">The new value.</param>
    /// <param name="propertyName">Name of the property used to notify listeners.
    ///     This value is optional and can be provided automatically when invoked from compilers
    ///     that support <see cref="CallerMemberNameAttribute"/>.</param>
    protected bool SetProperty<T>(ref T member, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(member, value)) return false;
        member = value;
        NotifyPropertyChanged(propertyName);
        HasChanges = true;
        return true;
    }

    #endregion INotifyPropertyChanged
}