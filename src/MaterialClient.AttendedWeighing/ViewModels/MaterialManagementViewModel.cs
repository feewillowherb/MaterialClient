using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Services;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.ViewModels;

/// <summary>
///     材料管理对话框 ViewModel
/// </summary>
public partial class MaterialManagementViewModel : ViewModelBase, ITransientDependency
{
    private readonly IMaterialService _materialService;
    private int _currentPage = 1;
    private int _totalCount;
    private int _totalPages = 1;

    public MaterialManagementViewModel(
        IMaterialService materialService,
        ILogger<MaterialManagementViewModel>? logger = null)
        : base(logger)
    {
        _materialService = materialService;
        Records = new ObservableCollection<Material>();
        CurrentPage = 1;
        TotalPages = 1;

        LoadDataCommand = ReactiveCommand.CreateFromTask(LoadDataAsync);
    }

    public ObservableCollection<Material> Records { get; }

    [Reactive] public string Name { get; set; } = string.Empty;

    public int PageSize => DefaultPageSize;

    private const int DefaultPageSize = 10;

    public int CurrentPage
    {
        get => _currentPage;
        set
        {
            if (_currentPage != value)
            {
                _currentPage = value;
                this.RaisePropertyChanged();
                _ = LoadDataAsync();
            }
        }
    }

    public int TotalCount
    {
        get => _totalCount;
        private set => this.RaiseAndSetIfChanged(ref _totalCount, value);
    }

    public int TotalPages
    {
        get => _totalPages;
        private set => this.RaiseAndSetIfChanged(ref _totalPages, value);
    }

    public ICommand LoadDataCommand { get; }

    private async Task LoadDataAsync()
    {
        try
        {
            var searchText = string.IsNullOrWhiteSpace(Name) ? null : Name.Trim();
            var result = await _materialService.GetPagedMaterialsAsync(
                searchText, CurrentPage, PageSize, null);

            Records.Clear();
            foreach (var row in result.Items)
                Records.Add(row);

            TotalCount = (int)result.TotalCount;
            TotalPages = result.TotalCount > 0
                ? (int)Math.Ceiling(result.TotalCount / (double)PageSize)
                : 1;

            if (CurrentPage > TotalPages && TotalPages > 0)
                CurrentPage = TotalPages;
            if (CurrentPage < 1)
                CurrentPage = 1;
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "加载材料分页数据失败。");
            Records.Clear();
            TotalCount = 0;
            TotalPages = 1;
            CurrentPage = 1;
        }
    }

    /// <summary>
    ///     分页变化命令（Ursa.Pagination 用）
    /// </summary>
    [ReactiveCommand]
    private Task PageChangeAsync() => LoadDataAsync();

    [ReactiveCommand]
    private Task QueryAsync()
    {
        CurrentPage = 1;
        return LoadDataAsync();
    }

    [ReactiveCommand]
    private void Close()
    {
        // View 订阅 CloseCommand 执行 Close(false)
    }
}
