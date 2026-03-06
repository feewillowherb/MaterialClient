using Microsoft.Extensions.Logging;
using ReactiveUI;

namespace MaterialClient.UI.ViewModels;

/// <summary>
///     Base class for ViewModels using ReactiveUI
/// </summary>
public class ViewModelBase : ReactiveObject
{
    /// <summary>
    ///     Logger instance (can be null if not injected)
    /// </summary>
    protected readonly ILogger? Logger;

    /// <summary>
    ///     Constructor with optional logger
    /// </summary>
    protected ViewModelBase(ILogger? logger = null)
    {
        Logger = logger;
    }
}

/// <summary>
///     Alias for ViewModelBase (kept for backward compatibility with existing code)
/// </summary>
public class ReactiveViewModelBase : ViewModelBase
{
}
