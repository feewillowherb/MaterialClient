using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.ViewModels;

/// <summary>
///     材料选择弹窗 ViewModel
/// </summary>
public partial class MaterialsSelectionPopupViewModel : ViewModelBase, ITransientDependency
{
    private const int DefaultPageSize = 10;

    private readonly IMaterialService? _materialService;

    private ObservableCollection<Material> _pagedMaterials = new();

    [Reactive] private string _searchText = string.Empty;

    [Reactive] private Material? _selectedMaterial;

    private int _currentPage = 1;

    private int _pageSize = DefaultPageSize;

    private int _totalCount;

    private int _totalPages;

    public MaterialsSelectionPopupViewModel(IServiceProvider? serviceProvider)
        : base(serviceProvider?.GetService<ILogger<MaterialsSelectionPopupViewModel>>())
    {
        if (serviceProvider != null)
        {
            _materialService = serviceProvider.GetRequiredService<IMaterialService>();
        }

        InitializeFiltering();

        // 初始加载数据
        _ = LoadDataAsync();
    }

    // 保留向后兼容的构造函数
    public MaterialsSelectionPopupViewModel()
        : this(null)
    {
    }

    public ObservableCollection<Material> PagedMaterials
    {
        get => _pagedMaterials;
        private set => this.RaiseAndSetIfChanged(ref _pagedMaterials, value);
    }

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

    public int PageSize
    {
        get => _pageSize;
        set
        {
            this.RaiseAndSetIfChanged(ref _pageSize, value);
            CurrentPage = 1; // 重置到第一页
            _ = LoadDataAsync();
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

    public string CurrentPageInfo => $"当前页:{CurrentPage}";

    public string TotalCountInfo => $"共{TotalCount}条记录";

    private void InitializeFiltering()
    {
        // 当搜索文本变化时，重新查询数据（300ms 防抖）
        this.WhenAnyValue(x => x.SearchText)
            .Throttle(TimeSpan.FromMilliseconds(300))
            .Subscribe(_1 =>
            {
                CurrentPage = 1;
                _ = LoadDataAsync();
            });
    }

    private async Task LoadDataAsync()
    {
        if (_materialService == null) return;

        try
        {
            // 使用 MaterialService 进行分页查询
            var result = await _materialService.GetPagedMaterialsAsync(
                searchText: string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
                pageIndex: CurrentPage,
                pageSize: PageSize
            );

            // 更新总数
            TotalCount = (int)result.TotalCount;

            // 计算总页数
            TotalPages = TotalCount > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 1;

            // 确保当前页在有效范围内
            if (_currentPage > TotalPages && TotalPages > 0)
            {
                _currentPage = TotalPages;
                this.RaisePropertyChanged(nameof(CurrentPage));
            }

            if (_currentPage < 1)
            {
                _currentPage = 1;
                this.RaisePropertyChanged(nameof(CurrentPage));
            }

            // 在 UI 线程上更新显示的数据
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                PagedMaterials.Clear();
                foreach (var material in result.Items)
                {
                    PagedMaterials.Add(material);
                }
                
                // 手动触发属性变更通知，确保 UI 更新
                this.RaisePropertyChanged(nameof(PagedMaterials));
            });

            this.RaisePropertyChanged(nameof(CurrentPageInfo));
            this.RaisePropertyChanged(nameof(TotalCountInfo));
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "加载材料列表失败");
            // 在 UI 线程上清空数据
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                PagedMaterials.Clear();
                this.RaisePropertyChanged(nameof(PagedMaterials));
            });
            TotalCount = 0;
            TotalPages = 1;
        }
    }

    /// <summary>
    ///     分页变化命令（用于 Ursa.Pagination 组件）
    ///     由于 CurrentPage 是双向绑定，页码变化会自动更新，此命令主要用于触发数据刷新
    /// </summary>
    [ReactiveCommand]
    private Task PageChangeAsync()
    {
        // CurrentPage 已经通过双向绑定自动更新，重新加载数据
        return LoadDataAsync();
    }

    // 保留原有命令以保持向后兼容（如果其他地方有引用）
    [ReactiveCommand]
    private Task FirstPageAsync()
    {
        if (CurrentPage > 1)
        {
            CurrentPage = 1;
        }

        return Task.CompletedTask;
    }

    [ReactiveCommand]
    private Task PreviousPageAsync()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
        }

        return Task.CompletedTask;
    }

    [ReactiveCommand]
    private Task NextPageAsync()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
        }

        return Task.CompletedTask;
    }

    [ReactiveCommand]
    private Task LastPageAsync()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage = TotalPages;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    ///     刷新材料列表（用于外部调用）
    /// </summary>
    public Task RefreshAsync()
    {
        return LoadDataAsync();
    }
}