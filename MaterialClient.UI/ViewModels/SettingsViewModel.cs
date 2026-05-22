using Avalonia.Controls;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using MaterialClient.UI.Abstractions;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.UI.ViewModels;

/// <summary>
///     Base ViewModel for the shared SettingsDialog.
///     Manages sections collection, selected section, and save command orchestration.
/// </summary>
public class SettingsViewModel : ReactiveObject, ITransientDependency
{
    private readonly ILogger<SettingsViewModel> _logger;
    private readonly List<ISettingsSection> _allSections = [];

    public SettingsViewModel(
        IEnumerable<ISettingsSection> sections,
        ILogger<SettingsViewModel> logger)
    {
        _logger = logger;

        foreach (var section in sections.OrderBy(s => s.DisplayName))
        {
            _allSections.Add(section);
            Sections.Add(section);
        }

        if (Sections.Count > 0)
        {
            SelectedSection = Sections[0];
        }

        var canSave = this.WhenAnyValue(x => x.SelectedSection)
            .Select(_ => _allSections.Any(s => s.IsDirty));
        SaveCommand = ReactiveCommand.CreateFromTask(SaveAllAsync, canSave);
    }

    [Reactive]
    public ObservableCollection<ISettingsSection> Sections { get; set; } = [];

    [Reactive]
    public ISettingsSection? SelectedSection { get; set; }

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }

    public bool IsAnyDirty => _allSections.Any(s => s.IsDirty);

    /// <summary>
    ///     Load all sections then refresh UI. Call before showing SettingsDialog.
    /// </summary>
    public async Task PrepareForDisplayAsync()
    {
        await LoadAllAsync();
    }

    public Control? CreateSelectedSectionView() => SelectedSection?.CreateView();

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
