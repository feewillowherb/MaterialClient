using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using MaterialClient.UI.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.UI.ViewModels;

/// <summary>
///     Base ViewModel for the shared SettingsDialog.
///     Manages sections collection, selected section, and save command orchestration.
///     Resolves all ISettingsSection implementations from the DI container.
/// </summary>
public class SettingsViewModel : ReactiveObject, ITransientDependency
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SettingsViewModel> _logger;
    private readonly List<ISettingsSection> _allSections = [];

    public SettingsViewModel(
        IServiceProvider serviceProvider,
        ILogger<SettingsViewModel> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        // Resolve all ISettingsSection implementations
        var sections = serviceProvider.GetServices<ISettingsSection>();
        foreach (var section in sections.OrderBy(s => s.DisplayName))
        {
            _allSections.Add(section);
            Sections.Add(section);
        }

        // Set first section as selected
        if (Sections.Count > 0)
        {
            SelectedSection = Sections[0];
        }

        // Save command: only enabled when any section is dirty
        var canSave = this.WhenAnyValue(x => x.SelectedSection)
            .Select(_ => _allSections.Any(s => s.IsDirty));
        SaveCommand = ReactiveCommand.CreateFromTask(SaveAllAsync, canSave);

        // Fire-and-forget load for all sections
        _ = LoadAllAsync();
    }

    /// <summary>
    ///     All registered settings sections.
    /// </summary>
    [Reactive]
    public ObservableCollection<ISettingsSection> Sections { get; set; } = [];

    /// <summary>
    ///     Currently selected settings section.
    /// </summary>
    [Reactive]
    public ISettingsSection? SelectedSection { get; set; }

    /// <summary>
    ///     Command to save all dirty sections.
    /// </summary>
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }

    /// <summary>
    ///     Whether any section has unsaved changes.
    /// </summary>
    public bool IsAnyDirty => _allSections.Any(s => s.IsDirty);

    private async Task LoadAllAsync()
    {
        foreach (var section in _allSections)
        {
            try
            {
                await section.LoadAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load settings section: {SectionName}", section.DisplayName);
            }
        }
    }

    private async Task SaveAllAsync()
    {
        foreach (var section in _allSections.Where(s => s.IsDirty))
        {
            try
            {
                await section.SaveAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save settings section: {SectionName}", section.DisplayName);
            }
        }
    }
}
