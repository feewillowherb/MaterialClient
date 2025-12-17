using ReactiveUI;

namespace MaterialClient.ViewModels;

/// <summary>
/// Base class for ViewModels using ReactiveUI
/// </summary>
public partial class ViewModelBase : ReactiveObject
{
}

/// <summary>
/// Alias for ViewModelBase (kept for backward compatibility)
/// </summary>
public partial class ReactiveViewModelBase : ViewModelBase
{
}