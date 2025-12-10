using CommunityToolkit.Mvvm.ComponentModel;
using ReactiveUI;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MaterialClient.ViewModels;

/// <summary>
/// Base class for ViewModels using CommunityToolkit.Mvvm and ReactiveUI
/// </summary>
public class ViewModelBase : ObservableObject, IReactiveObject
{
    private event PropertyChangingEventHandler? PropertyChangingHandler;

    // Implement INotifyPropertyChanging.PropertyChanging (inherited by IReactiveObject)
    public event PropertyChangingEventHandler? PropertyChanging
    {
        add => PropertyChangingHandler += value;
        remove => PropertyChangingHandler -= value;
    }

    // Implement IReactiveObject.RaisePropertyChanged
    void IReactiveObject.RaisePropertyChanged(PropertyChangedEventArgs args)
    {
        OnPropertyChanged(args);
    }

    // Implement IReactiveObject.RaisePropertyChanging
    void IReactiveObject.RaisePropertyChanging(PropertyChangingEventArgs args)
    {
        PropertyChangingHandler?.Invoke(this, args);
    }

    // Override SetProperty to ensure ReactiveUI compatibility
    // CommunityToolkit.Mvvm [ObservableProperty] will call this method
    protected new bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value))
            return false;

        PropertyChangingHandler?.Invoke(this, new PropertyChangingEventArgs(propertyName));
        field = value;
        OnPropertyChanged(propertyName);
        ((IReactiveObject)this).RaisePropertyChanged(new PropertyChangedEventArgs(propertyName));
        return true;
    }
}

/// <summary>
/// Alias for ViewModelBase using ReactiveUI (kept for backward compatibility)
/// </summary>
public class ReactiveViewModelBase : ViewModelBase
{
}