using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using System.Windows.Input;
using System.Collections;
using Volo.Abp.Application.Dtos;

namespace MaterialClient.UI.ViewModels;

public interface IGenericSelectionItem
{
    string DisplayText { get; }
}

public interface IGenericSelectionPopupBindings : IGenericSelectionPopupViewModel
{
    string SearchText { get; set; }

    /// <summary>
    /// Display text of the currently selected item (for closed-state display). Empty when none selected.
    /// </summary>
    string SelectedDisplayText { get; }

    IEnumerable PagedItems { get; }

    object? SelectedItem { get; set; }

    bool ShowResults { get; }

    bool ShowAddNewButton { get; }

    string AddNewButtonText { get; }

    int CurrentPage { get; set; }

    int PageSize { get; set; }

    int TotalCount { get; }

    string CurrentPageInfo { get; }

    string TotalCountInfo { get; }

    ICommand PageChangeCommand { get; }
}

public interface IGenericSelectionPopupViewModel
{
    ICommand SelectItemCommand { get; }

    ICommand AddNewItemCommand { get; }
}

public enum GenericSelectionPagingMode
{
    ClientSide,
    ServerSide
}

public sealed class GenericSelectionItem<T>
    : IGenericSelectionItem
{
    public required T Value { get; init; }
    public required string DisplayText { get; init; }
}

/// <summary>
/// Reusable selection popup VM with search + pagination.
/// - ClientSide: loads all items once, then filters/pages in-memory.
/// - ServerSide: queries page-by-page via provided delegate.
/// </summary>
public partial class GenericSelectionPopupViewModel<T> : ViewModelBase
    , IGenericSelectionPopupViewModel
    , IGenericSelectionPopupBindings
{
    private const int DefaultPageSize = 10;

    private readonly GenericSelectionPagingMode _pagingMode;
    private readonly Func<T, string> _displayTextSelector;
    private readonly Func<string?, int, int, IReadOnlyList<int>?, Task<PagedResultDto<T>>>? _loadPageFunc;
    private readonly Func<T, int?>? _getSelectedId;
    private readonly Func<Task<IReadOnlyList<T>>>? _loadAllFunc;
    private readonly Func<string, Task<T?>>? _createNewItemFunc;
    private readonly bool _allowAddNew;

    private IReadOnlyList<T> _allItems = Array.Empty<T>();

    private ObservableCollection<GenericSelectionItem<T>> _pagedItems = new();

    /// <summary>
    /// When set by the caller before RefreshAsync, these ids are used as selectedIds for the load
    /// (so the first page includes them) and selection is restored after the list is populated.
    /// Cleared after use so the grid does not clear SelectedItem before we read it.
    /// </summary>
    public IReadOnlyList<int>? PendingSelectedIds { get; set; }

    [Reactive] private string _searchText = string.Empty;
    [Reactive] private GenericSelectionItem<T>? _selectedItem;

    private int _currentPage = 1;
    private int _pageSize = DefaultPageSize;
    private int _totalCount;
    private int _totalPages = 1;

    public GenericSelectionPopupViewModel(
        GenericSelectionPagingMode pagingMode,
        Func<T, string> displayTextSelector,
        ILogger? logger = null,
        Func<string?, int, int, IReadOnlyList<int>?, Task<PagedResultDto<T>>>? loadPageFunc = null,
        Func<T, int?>? getSelectedId = null,
        Func<Task<IReadOnlyList<T>>>? loadAllFunc = null,
        Func<string, Task<T?>>? createNewItemFunc = null,
        int pageSize = DefaultPageSize,
        bool allowAddNew = true)
        : base(logger)
    {
        _pagingMode = pagingMode;
        _displayTextSelector = displayTextSelector;
        _loadPageFunc = loadPageFunc;
        _getSelectedId = getSelectedId;
        _loadAllFunc = loadAllFunc;
        _createNewItemFunc = createNewItemFunc;
        _allowAddNew = allowAddNew;

        _pageSize = pageSize <= 0 ? DefaultPageSize : pageSize;

        InitializeFiltering();
    }

    public ObservableCollection<GenericSelectionItem<T>> PagedItems
    {
        get => _pagedItems;
        private set => this.RaiseAndSetIfChanged(ref _pagedItems, value);
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
                _ = RefreshAsync();
            }
        }
    }

    public int PageSize
    {
        get => _pageSize;
        set
        {
            this.RaiseAndSetIfChanged(ref _pageSize, value);
            CurrentPage = 1;
            _ = RefreshAsync();
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

    public T? SelectedValue => SelectedItem != null ? SelectedItem.Value : default;

    public string SelectedDisplayText => SelectedItem?.DisplayText ?? string.Empty;

    public bool ShowResults => TotalCount > 0;

    public bool ShowAddNewButton => _allowAddNew && TotalCount == 0 && !string.IsNullOrWhiteSpace(SearchText);

    public string AddNewButtonText
    {
        get
        {
            return "新增";
        }
    }

    private void InitializeFiltering()
    {
        this.WhenAnyValue(x => x.SearchText)
            .Throttle(TimeSpan.FromMilliseconds(300))
            .Subscribe(_1 =>
            {
                CurrentPage = 1;
                _ = RefreshAsync();
            });

        this.WhenAnyValue(x => x.SelectedItem)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(SelectedValue));
                this.RaisePropertyChanged(nameof(SelectedDisplayText));
            });
    }

    /// <summary>
    /// For client-side mode: load all items once before first use.
    /// For server-side mode: optional (no-op).
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_pagingMode != GenericSelectionPagingMode.ClientSide)
        {
            await RefreshAsync();
            return;
        }

        if (_loadAllFunc == null)
        {
            _allItems = Array.Empty<T>();
            await RefreshAsync();
            return;
        }

        try
        {
            _allItems = await _loadAllFunc();
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "初始化选择弹窗数据失败");
            _allItems = Array.Empty<T>();
        }

        await RefreshAsync();
    }

    public Task RefreshAsync()
    {
        return LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            if (_pagingMode == GenericSelectionPagingMode.ServerSide)
            {
                if (_loadPageFunc == null)
                {
                    await SetItemsAsync(totalCount: 0, items: Array.Empty<T>());
                    return;
                }

                IReadOnlyList<int>? selectedIds = null;
                if (PendingSelectedIds != null && PendingSelectedIds.Count > 0)
                    selectedIds = PendingSelectedIds;
                else if (_getSelectedId != null && SelectedItem != null)
                {
                    var id = _getSelectedId(SelectedItem.Value);
                    if (id.HasValue)
                        selectedIds = new List<int> { id.Value };
                }

                var result = await _loadPageFunc(
                    string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
                    CurrentPage,
                    PageSize,
                    selectedIds);

                TotalCount = (int)result.TotalCount;
                TotalPages = TotalCount > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 1;

                await SetItemsAsync(TotalCount, result.Items, selectedIds);
                return;
            }

            // Client-side
            var filtered = FilterClientSide(_allItems, SearchText);
            TotalCount = filtered.Count;
            TotalPages = TotalCount > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 1;

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

            var page = filtered
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            await SetItemsAsync(TotalCount, page);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "加载选择弹窗数据失败");
            TotalCount = 0;
            TotalPages = 1;
            await SetItemsAsync(totalCount: 0, items: Array.Empty<T>());
        }
        finally
        {
            this.RaisePropertyChanged(nameof(CurrentPageInfo));
            this.RaisePropertyChanged(nameof(TotalCountInfo));
            this.RaisePropertyChanged(nameof(ShowResults));
            this.RaisePropertyChanged(nameof(ShowAddNewButton));
            this.RaisePropertyChanged(nameof(AddNewButtonText));
        }
    }

    private List<T> FilterClientSide(IReadOnlyList<T> items, string? searchText)
    {
        if (items.Count == 0) return new List<T>();
        if (string.IsNullOrWhiteSpace(searchText)) return items.ToList();

        var search = searchText.Trim();
        return items
            .Where(x =>
            {
                var text = _displayTextSelector(x) ?? string.Empty;
                return text.Contains(search, StringComparison.OrdinalIgnoreCase);
            })
            .ToList();
    }

    private async Task SetItemsAsync(long totalCount, IReadOnlyList<T> items, IReadOnlyList<int>? selectedIdsToRestore = null)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            PagedItems.Clear();
            foreach (var item in items)
            {
                PagedItems.Add(new GenericSelectionItem<T>
                {
                    Value = item,
                    DisplayText = _displayTextSelector(item) ?? string.Empty
                });
            }

            this.RaisePropertyChanged(nameof(PagedItems));

            if (selectedIdsToRestore != null && selectedIdsToRestore.Count > 0 && _getSelectedId != null)
            {
                var id = selectedIdsToRestore[0];
                var wrapper = PagedItems.FirstOrDefault(w => _getSelectedId(w.Value) == id);
                if (wrapper != null)
                {
                    SelectedItem = wrapper;
                    Dispatcher.UIThread.Post(() => SelectedItem = wrapper, DispatcherPriority.Loaded);
                }
                PendingSelectedIds = null;
            }
        });
    }

    [ReactiveCommand]
    private Task PageChangeAsync()
    {
        return RefreshAsync();
    }

    [ReactiveCommand]
    private Task SelectItemAsync(GenericSelectionItem<T>? item)
    {
        if (item != null)
        {
            SelectedItem = item;
        }

        return Task.CompletedTask;
    }

    ICommand IGenericSelectionPopupViewModel.SelectItemCommand => SelectItemCommand;

    /// <summary>
    /// Creatable pattern: insert into list first, then set selection (see docs/design-creatable-selection-react-select.md).
    /// </summary>
    [ReactiveCommand]
    private async Task AddNewItemAsync()
    {
        if (_createNewItemFunc == null)
        {
            return;
        }

        var name = SearchText?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        try
        {
            var newItem = await _createNewItemFunc(name);
            if (newItem == null)
            {
                return;
            }

            var wrapper = new GenericSelectionItem<T>
            {
                Value = newItem,
                DisplayText = _displayTextSelector(newItem) ?? string.Empty
            };

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                PagedItems.Insert(0, wrapper);
                TotalCount += 1;
                TotalPages = TotalCount > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 1;
                this.RaisePropertyChanged(nameof(TotalCountInfo));
                this.RaisePropertyChanged(nameof(CurrentPageInfo));
                this.RaisePropertyChanged(nameof(ShowResults));
                this.RaisePropertyChanged(nameof(ShowAddNewButton));
                SelectedItem = wrapper;
                // If the grid clears selection when the collection updates, restore it on the next frame.
                Dispatcher.UIThread.Post(() => SelectedItem = wrapper, DispatcherPriority.Loaded);
            });
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "新增数据失败");
        }
    }

    ICommand IGenericSelectionPopupViewModel.AddNewItemCommand => AddNewItemCommand;

    IEnumerable IGenericSelectionPopupBindings.PagedItems => PagedItems;

    object? IGenericSelectionPopupBindings.SelectedItem
    {
        get => SelectedItem;
        set => SelectedItem = value as GenericSelectionItem<T>;
    }

    ICommand IGenericSelectionPopupBindings.PageChangeCommand => PageChangeCommand;

    string IGenericSelectionPopupBindings.SelectedDisplayText => SelectedDisplayText;
}

