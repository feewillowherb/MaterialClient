using Avalonia.Controls;

namespace MaterialClient.UI.Abstractions;

/// <summary>
///     Interface for a settings section that can be displayed in the shared SettingsDialog.
///     Each consuming app implements this for its own settings categories.
/// </summary>
public interface ISettingsSection
{
    /// <summary>
    ///     Display name shown in the navigation sidebar.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    ///     Create the Avalonia view for this settings section.
    /// </summary>
    Control CreateView();

    /// <summary>
    ///     Load current settings values from storage.
    /// </summary>
    Task LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Save modified settings values to storage.
    /// </summary>
    Task SaveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Whether this section has unsaved changes.
    /// </summary>
    bool IsDirty { get; }
}
