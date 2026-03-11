using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MaterialClient.Common.Api.Dtos;
using Volo.Abp.Application.Dtos;

namespace MaterialClient.UI.Views.Controls;

/// <summary>
/// 可创建、可分页、可搜索的单一选择控件。内嵌 PART_TextBox + PART_Popup（列表、分页、可选“新增”）。
/// </summary>
public class CreatablePageableSearchableSelectionBox : TemplatedControl
{
    private const int DebounceMs = 300;

    private string _searchText = string.Empty;
    private int _currentPage = 1;
    private long _totalCount;
    private CancellationTokenSource? _debounceCts;
    private IDisposable? _isPopupOpenSub;
    private IDisposable? _selectedItemSub;
    public static readonly StyledProperty<bool> IsPopupOpenProperty =
        AvaloniaProperty.Register<CreatablePageableSearchableSelectionBox, bool>(nameof(IsPopupOpen), defaultValue: false);

    public static readonly StyledProperty<SelectionItem?> SelectedItemProperty =
        AvaloniaProperty.Register<CreatablePageableSearchableSelectionBox, SelectionItem?>(nameof(SelectedItem), defaultValue: null);

    public static readonly StyledProperty<string?> WatermarkProperty =
        AvaloniaProperty.Register<CreatablePageableSearchableSelectionBox, string?>(nameof(Watermark), defaultValue: "请选择");

    public static readonly StyledProperty<int> PageSizeProperty =
        AvaloniaProperty.Register<CreatablePageableSearchableSelectionBox, int>(nameof(PageSize), defaultValue: 10);

    /// <summary>
    /// 分页加载委托：(searchText, page, pageSize, selectedIds, ct) => Task(PagedResultDto)
    /// </summary>
    public static readonly StyledProperty<Func<string?, int, int, IReadOnlyList<int>?, CancellationToken, Task<PagedResultDto<SelectionItem>?>?>?> LoadPageAsyncProperty =
        AvaloniaProperty.Register<CreatablePageableSearchableSelectionBox, Func<string?, int, int, IReadOnlyList<int>?, CancellationToken, Task<PagedResultDto<SelectionItem>?>?>?>(nameof(LoadPageAsync), defaultValue: null);

    public static readonly StyledProperty<object?> AddNewCommandProperty =
        AvaloniaProperty.Register<CreatablePageableSearchableSelectionBox, object?>(nameof(AddNewCommand), defaultValue: null);

    /// <summary>当前页展示项，供模板 PART_ItemsList 绑定。</summary>
    public ObservableCollection<SelectionItem> CurrentPageItems { get; } = new();

    /// <summary>是否显示空状态与“新增”入口。</summary>
    public static readonly StyledProperty<bool> ShowAddNewProperty =
        AvaloniaProperty.Register<CreatablePageableSearchableSelectionBox, bool>(nameof(ShowAddNew), defaultValue: false);

    public bool ShowAddNew
    {
        get => GetValue(ShowAddNewProperty);
        set => SetValue(ShowAddNewProperty, value);
    }

    public bool IsPopupOpen
    {
        get => GetValue(IsPopupOpenProperty);
        set => SetValue(IsPopupOpenProperty, value);
    }

    public SelectionItem? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public string? Watermark
    {
        get => GetValue(WatermarkProperty);
        set => SetValue(WatermarkProperty, value);
    }

    public int PageSize
    {
        get => GetValue(PageSizeProperty);
        set => SetValue(PageSizeProperty, value);
    }

    public Func<string?, int, int, IReadOnlyList<int>?, CancellationToken, Task<PagedResultDto<SelectionItem>?>?>? LoadPageAsync
    {
        get => GetValue(LoadPageAsyncProperty);
        set => SetValue(LoadPageAsyncProperty, value);
    }

    public object? AddNewCommand
    {
        get => GetValue(AddNewCommandProperty);
        set => SetValue(AddNewCommandProperty, value);
    }

    internal TextBox? PART_TextBox { get; private set; }
    internal Popup? PART_Popup { get; private set; }
    internal Border? PART_RootBorder { get; private set; }
    internal ListBox? PART_ItemsList { get; private set; }
    internal Panel? PART_Pager { get; private set; }
    internal Button? PART_LoadMoreButton { get; private set; }
    internal Panel? PART_EmptyPanel { get; private set; }
    internal Button? PART_AddNewButton { get; private set; }

    public CreatablePageableSearchableSelectionBox()
    {
        Height = 32;
        MinHeight = 32;
        Focusable = true;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _isPopupOpenSub?.Dispose();
        _selectedItemSub?.Dispose();

        PART_TextBox = e.NameScope.Find<TextBox>("PART_TextBox");
        PART_Popup = e.NameScope.Find<Popup>("PART_Popup");
        PART_RootBorder = e.NameScope.Find<Border>("PART_RootBorder");
        PART_ItemsList = e.NameScope.Find<ListBox>("PART_ItemsList");
        PART_Pager = e.NameScope.Find<Panel>("PART_Pager");
        PART_LoadMoreButton = e.NameScope.Find<Button>("PART_LoadMoreButton");
        PART_EmptyPanel = e.NameScope.Find<Panel>("PART_EmptyPanel");
        PART_AddNewButton = e.NameScope.Find<Button>("PART_AddNewButton");

        if (PART_LoadMoreButton != null)
            PART_LoadMoreButton.Click += OnLoadMoreClick;

        if (PART_RootBorder != null)
            PART_RootBorder.PointerPressed += OnRootPointerPressed;
        if (PART_TextBox != null)
        {
            PART_TextBox.TextChanged += OnTextBoxTextChanged;
            PART_TextBox.KeyDown += OnTextBoxKeyDown;
        }
        if (PART_ItemsList != null)
        {
            PART_ItemsList.SelectionChanged += OnItemsListSelectionChanged;
            PART_ItemsList.KeyDown += OnItemsListKeyDown;
            PART_ItemsList.DoubleTapped += OnItemsListDoubleTapped;
        }
        if (PART_Popup != null)
            PART_Popup.Closed += OnPopupClosed;

        _isPopupOpenSub = this.GetObservable(IsPopupOpenProperty).Subscribe(OnIsPopupOpenChanged);
        _selectedItemSub = this.GetObservable(SelectedItemProperty).Subscribe(_ => UpdateTextBoxFromSelectedItem());

        UpdateTextBoxFromSelectedItem();
    }

    protected override void OnGotFocus(GotFocusEventArgs e)
    {
        base.OnGotFocus(e);
        if (!IsPopupOpen)
            IsPopupOpen = true;
    }

    private void OnRootPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsPopupOpen)
            IsPopupOpen = true;
    }

    private void OnIsPopupOpenChanged(bool isOpen)
    {
        if (isOpen)
        {
            _searchText = SelectedItem?.Name ?? string.Empty;
            SyncTextBoxToSearchText();
            _currentPage = 1;
            _ = LoadPageAsyncInternalAsync();
            Dispatcher.UIThread.Post(() => PART_TextBox?.Focus(), DispatcherPriority.Loaded);
        }
        else
        {
            ResetSearchTextToSelectedItem();
            SyncTextBoxToSearchText();
        }
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        ResetSearchTextToSelectedItem();
        SyncTextBoxToSearchText();
    }

    private void OnTextBoxTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (PART_TextBox == null) return;
        _searchText = PART_TextBox.Text ?? string.Empty;
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var ct = _debounceCts.Token;
        _ = Task.Run(async () =>
        {
            await Task.Delay(DebounceMs, ct).ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!IsPopupOpen) IsPopupOpen = true;
                _currentPage = 1;
                _ = LoadPageAsyncInternalAsync();
            });
        }, ct);
    }

    private void OnTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            IsPopupOpen = false;
            e.Handled = true;
        }
    }

    private void OnItemsListSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (PART_ItemsList?.SelectedItem is SelectionItem item)
        {
            SelectedItem = item;
            IsPopupOpen = false;
            Dispatcher.UIThread.Post(() => PART_TextBox?.Focus(), DispatcherPriority.Loaded);
        }
    }

    private void OnItemsListKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && PART_ItemsList?.SelectedItem is SelectionItem item)
        {
            SelectedItem = item;
            IsPopupOpen = false;
            Dispatcher.UIThread.Post(() => PART_TextBox?.Focus(), DispatcherPriority.Loaded);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            IsPopupOpen = false;
            e.Handled = true;
        }
    }

    private void OnItemsListDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (PART_ItemsList?.SelectedItem is SelectionItem item)
        {
            SelectedItem = item;
            IsPopupOpen = false;
            Dispatcher.UIThread.Post(() => PART_TextBox?.Focus(), DispatcherPriority.Loaded);
        }
    }

    private void ResetSearchTextToSelectedItem()
    {
        _searchText = SelectedItem?.Name ?? string.Empty;
    }

    private void SyncTextBoxToSearchText()
    {
        if (PART_TextBox != null)
            PART_TextBox.Text = _searchText;
    }

    private void UpdateTextBoxFromSelectedItem()
    {
        if (!IsPopupOpen && PART_TextBox != null)
        {
            _searchText = SelectedItem?.Name ?? string.Empty;
            PART_TextBox.Text = string.IsNullOrEmpty(_searchText) ? (Watermark ?? string.Empty) : _searchText;
        }
    }

    private IReadOnlyList<int>? GetSelectedIds()
    {
        var sel = SelectedItem;
        if (sel == null) return null;
        return new[] { sel.Id };
    }

    private async void OnLoadMoreClick(object? sender, RoutedEventArgs e)
    {
        _currentPage++;
        await LoadPageAsyncInternalAsync(append: true);
    }

    private async Task LoadPageAsyncInternalAsync(bool append = false)
    {
        var load = LoadPageAsync;
        if (load == null) return;
        if (!append)
        {
            CurrentPageItems.Clear();
            ShowAddNew = false;
        }
        try
        {
            var ct = CancellationToken.None;
            var selectedIds = GetSelectedIds();
            var result = await load(_searchText, _currentPage, PageSize, selectedIds, ct).ConfigureAwait(true);
            if (result is not { } r) return;
            _totalCount = r.TotalCount;
            foreach (var item in r.Items ?? Array.Empty<SelectionItem>())
                CurrentPageItems.Add(item);
            if (!append && CurrentPageItems.Count == 0)
                ShowAddNew = true;
        }
        catch
        {
            ShowAddNew = true;
        }
    }
}
