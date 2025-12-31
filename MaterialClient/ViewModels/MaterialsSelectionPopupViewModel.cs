using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using MaterialClient.Common.Entities;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace MaterialClient.ViewModels;

/// <summary>
///     材料选择弹窗 ViewModel
/// </summary>
public partial class MaterialsSelectionPopupViewModel : ViewModelBase
{
    private const int DefaultPageSize = 10;

    [Reactive] private ObservableCollection<Material> _allMaterials = new();

    private ObservableCollection<Material> _filteredMaterials = new();

    private ObservableCollection<Material> _pagedMaterials = new();

    [Reactive] private string _searchText = string.Empty;

    [Reactive] private Material? _selectedMaterial;

    private int _currentPage = 1;

    private int _pageSize = DefaultPageSize;

    private int _totalCount;

    private int _totalPages;

    public MaterialsSelectionPopupViewModel(ObservableCollection<Material> materials)
    {
        AllMaterials = materials;
        InitializeFiltering();
    }

    public MaterialsSelectionPopupViewModel()
    {
        AllMaterials = new ObservableCollection<Material>();
        InitializeFiltering();
    }

    public ObservableCollection<Material> FilteredMaterials
    {
        get => _filteredMaterials;
        private set => this.RaiseAndSetIfChanged(ref _filteredMaterials, value);
    }

    public ObservableCollection<Material> PagedMaterials
    {
        get => _pagedMaterials;
        private set => this.RaiseAndSetIfChanged(ref _pagedMaterials, value);
    }

    public int CurrentPage
    {
        get => _currentPage;
        private set => this.RaiseAndSetIfChanged(ref _currentPage, value);
    }

    public int PageSize
    {
        get => _pageSize;
        set
        {
            this.RaiseAndSetIfChanged(ref _pageSize, value);
            UpdatePagination();
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
        // 当搜索文本或所有材料列表变化时，重新过滤
        this.WhenAnyValue(x => x.SearchText, x => x.AllMaterials)
            .Throttle(TimeSpan.FromMilliseconds(300))
            .Subscribe(_ => ApplyFilter());

        // 当过滤后的材料列表变化时，更新分页
        this.WhenAnyValue(x => x.FilteredMaterials)
            .Subscribe(_ => UpdatePagination());
    }

    private void ApplyFilter()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            FilteredMaterials = new ObservableCollection<Material>(AllMaterials);
        }
        else
        {
            var searchLower = SearchText.ToLowerInvariant();
            var filtered = AllMaterials.Where(m =>
                (m.Name?.ToLowerInvariant().Contains(searchLower) ?? false) ||
                (m.Specifications?.ToLowerInvariant().Contains(searchLower) ?? false) ||
                (m.Size?.ToLowerInvariant().Contains(searchLower) ?? false) ||
                (m.Code?.ToLowerInvariant().Contains(searchLower) ?? false)
            ).ToList();

            FilteredMaterials = new ObservableCollection<Material>(filtered);
        }

        CurrentPage = 1; // 重置到第一页
    }

    private void UpdatePagination()
    {
        TotalCount = FilteredMaterials.Count;
        TotalPages = TotalCount > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 1;

        // 确保当前页在有效范围内
        if (CurrentPage > TotalPages && TotalPages > 0) CurrentPage = TotalPages;
        if (CurrentPage < 1) CurrentPage = 1;

        // 计算分页数据
        var startIndex = (CurrentPage - 1) * PageSize;
        var endIndex = Math.Min(startIndex + PageSize, TotalCount);
        var paged = FilteredMaterials.Skip(startIndex).Take(PageSize).ToList();

        PagedMaterials = new ObservableCollection<Material>(paged);

        this.RaisePropertyChanged(nameof(CurrentPageInfo));
        this.RaisePropertyChanged(nameof(TotalCountInfo));
    }

    [ReactiveCommand]
    private Task FirstPageAsync()
    {
        if (CurrentPage > 1)
        {
            CurrentPage = 1;
            UpdatePagination();
        }
        return Task.CompletedTask;
    }

    [ReactiveCommand]
    private Task PreviousPageAsync()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            UpdatePagination();
        }
        return Task.CompletedTask;
    }

    [ReactiveCommand]
    private Task NextPageAsync()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            UpdatePagination();
        }
        return Task.CompletedTask;
    }

    [ReactiveCommand]
    private Task LastPageAsync()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage = TotalPages;
            UpdatePagination();
        }
        return Task.CompletedTask;
    }

    /// <summary>
    ///     更新材料列表
    /// </summary>
    public void UpdateMaterials(ObservableCollection<Material> materials)
    {
        AllMaterials = materials;
    }
}

