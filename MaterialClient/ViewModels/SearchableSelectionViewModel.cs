using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using System.Windows.Input;
using Volo.Abp.Application.Dtos;

namespace MaterialClient.ViewModels;

/// <summary>
/// Interface for the new unified searchable selection component
/// </summary>
public interface ISearchableSelection<T>
{
    string SearchText { get; set; }
    T? SelectedValue { get; }
    string SelectedDisplayText { get; }
    ObservableCollection<SearchableSelectionItem<T>> PagedItems { get; }
    SearchableSelectionItem<T>? SelectedItem { get; set; }
    bool IsPopupOpen { get; set; }
    int CurrentPage { get; set; }
    int PageSize { get; set; }
    int TotalCount { get; }
    ICommand SelectItemCommand { get; }
    ICommand AddNewItemCommand { get; }
    ICommand PageChangeCommand { get; }
    Task InitializeAsync();
    Task RefreshAsync();
}

/// <summary>
/// Configuration interface for SearchableSelectionViewModel
/// </summary>
public interface ISearchableSelectionConfig<T>
{
    Func<T, string> DisplayTextSelector { get; }
    Func<T, int?>? GetIdSelector { get; }
    Func<string?, int, int, IReadOnlyList<int>?, Task<PagedResultDto<T>>>? LoadPageFunc { get; }
    Func<Task<IReadOnlyList<T>>>? LoadAllFunc { get; }
    Func<string, Task<T?>>? CreateNewItemFunc { get; }
    bool AllowAddNew { get; }
    int PageSize { get; }
}

/// <summary>
/// Simple configuration record for SearchableSelectionViewModel
/// </summary>
public sealed record SearchableSelectionConfig<T> : ISearchableSelectionConfig<T>
{
    public required Func<T, string> DisplayTextSelector { get; init; }
    public Func<T, int?>? GetIdSelector { get; init; }
    public Func<string?, int, int, IReadOnlyList<int>?, Task<PagedResultDto<T>>>? LoadPageFunc { get; init; }
    public Func<Task<IReadOnlyList<T>>>? LoadAllFunc { get; init; }
    public Func<string, Task<T?>>? CreateNewItemFunc { get; init; }
    public bool AllowAddNew { get; init; } = true;
    public int PageSize { get; init; } = 10;
}

/// <summary>
/// Wrapper item for searchable selection
/// </summary>
public sealed class SearchableSelectionItem<T>
{
    public required T Value { get; init; }
    public required string DisplayText { get; init; }
}

/// <summary>
/// Paging mode for searchable selection
/// </summary>
public enum SearchableSelectionPagingMode
{
    ClientSide,
    ServerSide
}

/// <summary>
/// Unified searchable selection ViewModel with search + pagination + create new functionality.
/// - ClientSide: loads all items once, then filters/pages in-memory
/// - ServerSide: queries page-by-page via ABP application services
/// </summary>
public sealed partial class SearchableSelectionViewModel<T> : ViewModelBase, ISearchableSelection<T>, IDisposable
{
    private const int DefaultPageSize = 10;

    private readonly SearchableSelectionPagingMode _pagingMode;
    private readonly Func<T, string> _displayTextSelector;
    private readonly Func<string?, int, int, IReadOnlyList<int>?, Task<PagedResultDto<T>>>? _loadPageFunc;
    private readonly Func<T, int?>? _getIdSelector;
    private readonly Func<Task<IReadOnlyList<T>>>? _loadAllFunc;
    private readonly Func<string, Task<T?>>? _createNewItemFunc;
    private readonly bool _allowAddNew;
    private readonly CompositeDisposable _disposables = new();

    private IReadOnlyList<T> _allItems = Array.Empty<T>();
    private ObservableCollection<SearchableSelectionItem<T>> _pagedItems = new();

    /// <summary>
    /// Pending selected IDs to be restored after data loading
    /// </summary>
    public IReadOnlyList<int>? PendingSelectedIds { get; set; }

    [Reactive] private string _searchText = string.Empty;
    [Reactive] private SearchableSelectionItem<T>? _selectedItem;
    [Reactive] private bool _isPopupOpen;
    [Reactive] private int _currentPage = 1;
    [Reactive] private int _pageSize = DefaultPageSize;
    [Reactive] private int _totalCount;
    [Reactive] private int _totalPages = 1;

    public SearchableSelectionViewModel(
        SearchableSelectionPagingMode pagingMode,
        Func<T, string> displayTextSelector,
        ILogger? logger = null,
        Func<string?, int, int, IReadOnlyList<int>?, Task<PagedResultDto<T>>>? loadPageFunc = null,
        Func<T, int?>? getIdSelector = null,
        Func<Task<IReadOnlyList<T>>>? loadAllFunc = null,
        Func<string, Task<T?>>? createNewItemFunc = null,
        int pageSize = DefaultPageSize,
        bool allowAddNew = true)
        : base(logger)
    {
        _pagingMode = pagingMode;
        _displayTextSelector = displayTextSelector;
        _loadPageFunc = loadPageFunc;
        _getIdSelector = getIdSelector;
        _loadAllFunc = loadAllFunc;
        _createNewItemFunc = createNewItemFunc;
        _allowAddNew = allowAddNew;

        _pageSize = pageSize <= 0 ? DefaultPageSize : pageSize;

        InitializeReactiveChains();
    }

    /// <summary>
    /// Constructor with configuration interface
    /// </summary>
    public SearchableSelectionViewModel(ISearchableSelectionConfig<T> config, ILogger? logger = null)
        : this(
            DeterminePagingMode(config),
            config.DisplayTextSelector,
            logger,
            config.LoadPageFunc,
            config.GetIdSelector,
            config.LoadAllFunc,
            config.CreateNewItemFunc,
            config.PageSize,
            config.AllowAddNew)
    {
    }

    private static SearchableSelectionPagingMode DeterminePagingMode(ISearchableSelectionConfig<T> config)
    {
        // If LoadPageFunc is provided, use ServerSide; otherwise ClientSide
        return config.LoadPageFunc != null ? SearchableSelectionPagingMode.ServerSide : SearchableSelectionPagingMode.ClientSide;
    }

    public ObservableCollection<SearchableSelectionItem<T>> PagedItems
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

    public T? SelectedValue => SelectedItem?.Value ?? default;

    public string SelectedDisplayText => SelectedItem?.DisplayText ?? string.Empty;

    public bool ShowResults => TotalCount > 0;

    public bool ShowAddNewButton => _allowAddNew && TotalCount == 0 && !string.IsNullOrWhiteSpace(SearchText);

    public string AddNewButtonText => "新增";

    private void InitializeReactiveChains()
    {
        // Search throttling with 300ms debounce
        this.WhenAnyValue(x => x.SearchText)
            .Throttle(TimeSpan.FromMilliseconds(300))
            .ObserveOn(RxApp.TaskpoolScheduler)
            .SelectMany(_ => LoadDataAsync().ToObservable())
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(PagedItems));
                this.RaisePropertyChanged(nameof(CurrentPageInfo));
                this.RaisePropertyChanged(nameof(TotalCountInfo));
                this.RaisePropertyChanged(nameof(ShowResults));
                this.RaisePropertyChanged(nameof(ShowAddNewButton));
            })
            .DisposeWith(_disposables);

        // Selection state propagation
        this.WhenAnyValue(x => x.SelectedItem)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(SelectedValue));
                this.RaisePropertyChanged(nameof(SelectedDisplayText));

                // Auto-close popup after selection if configured
                if (SelectedItem != null && CloseOnSelect)
                {
                    IsPopupOpen = false;
                }
            })
            .DisposeWith(_disposables);

        // Popup state management
        this.WhenAnyValue(x => x.IsPopupOpen)
            .Subscribe(isOpen =>
            {
                if (isOpen)
                {
                    // Reset to first page when opening
                    CurrentPage = 1;
                    _ = RefreshAsync();
                }
            })
            .DisposeWith(_disposables);
    }

    /// <summary>
    /// Close popup after selection (default: true)
    /// </summary>
    public bool CloseOnSelect { get; set; } = true;

    /// <summary>
    /// For client-side mode: load all items once before first use.
    /// For server-side mode: optional (no-op).
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_pagingMode != SearchableSelectionPagingMode.ClientSide)
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
            Logger?.LogError(ex, "初始化选择组件数据失败");
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
            if (_pagingMode == SearchableSelectionPagingMode.ServerSide)
            {
                if (_loadPageFunc == null)
                {
                    await SetItemsAsync(totalCount: 0, items: Array.Empty<T>());
                    return;
                }

                IReadOnlyList<int>? selectedIds = null;
                if (PendingSelectedIds != null && PendingSelectedIds.Count > 0)
                    selectedIds = PendingSelectedIds;
                else if (_getIdSelector != null && SelectedItem != null)
                {
                    var id = _getIdSelector(SelectedItem.Value);
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
            Logger?.LogError(ex, "加载选择组件数据失败");
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
                PagedItems.Add(new SearchableSelectionItem<T>
                {
                    Value = item,
                    DisplayText = _displayTextSelector(item) ?? string.Empty
                });
            }

            this.RaisePropertyChanged(nameof(PagedItems));

            if (selectedIdsToRestore != null && selectedIdsToRestore.Count > 0 && _getIdSelector != null)
            {
                var id = selectedIdsToRestore[0];
                var wrapper = PagedItems.FirstOrDefault(w => _getIdSelector(w.Value) == id);
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
    private Task SelectItemAsync(SearchableSelectionItem<T>? item)
    {
        if (item != null)
        {
            SelectedItem = item;
        }

        return Task.CompletedTask;
    }

    ICommand ISearchableSelection<T>.SelectItemCommand => SelectItemCommand;

    /// <summary>
    /// Creatable pattern: insert into list first, then set selection
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

            var wrapper = new SearchableSelectionItem<T>
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

    ICommand ISearchableSelection<T>.AddNewItemCommand => AddNewItemCommand;
    ICommand ISearchableSelection<T>.PageChangeCommand => PageChangeCommand;

    public void Dispose()
    {
        _disposables.Dispose();
    }
}
