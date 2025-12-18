using ReactiveUI;
using Microsoft.Extensions.Logging;

namespace MaterialClient.ViewModels;

/// <summary>
/// Base class for ViewModels using ReactiveUI
/// </summary>
public partial class ViewModelBase : ReactiveObject
{
    /// <summary>
    /// Logger instance (can be null if not injected)
    /// </summary>
    protected readonly ILogger? Logger;

    /// <summary>
    /// Constructor with optional logger
    /// </summary>
    protected ViewModelBase(ILogger? logger = null)
    {
        Logger = logger;
    }
}

/// <summary>
/// Alias for ViewModelBase (kept for backward compatibility)
/// </summary>
public partial class ReactiveViewModelBase : ViewModelBase
{
}