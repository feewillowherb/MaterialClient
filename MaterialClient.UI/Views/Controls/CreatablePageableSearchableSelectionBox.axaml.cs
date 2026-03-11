using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;
using MaterialClient.Common.Api.Dtos;
using Volo.Abp.Application.Dtos;

namespace MaterialClient.UI.Views.Controls;

/// <summary>
/// 可创建、可分页、可搜索的单一选择控件。
/// 内嵌 PART_TextBox + PART_Popup（DataGrid、分页、可选"新增"），视觉与 GenericSelectionPopup 一致。
/// </summary>
public class CreatablePageableSearchableSelectionBox : TemplatedControl
{
    private const int DebounceMs = 300;

    private string _searchText = string.Empty;
    private CancellationTokenSource? _debounceCts;
    private bool _suppressPageChangeLoad;

    private IDisposable? _isPopupOpenSub;
    private IDisposable? _selectedItemSub;
    private IDisposable? _currentPageSub;

    #region Styled Properties

    public static readonly StyledProperty<bool> IsPopupOpenProperty =
        AvaloniaProperty.Register<CreatablePageableSearchableSelectionBox, bool>(nameof(IsPopupOpen));

    public static readonly StyledProperty<SelectionItem?> SelectedItemProperty =
        AvaloniaProperty.Register<CreatablePageableSearchableSelectionBox, SelectionItem?>(nameof(SelectedItem));

    public static readonly StyledProperty<string?> WatermarkProperty =
        AvaloniaProperty.Register<CreatablePageableSearchableSelectionBox, string?>(nameof(Watermark), defaultValue: "请选择");

    public static readonly StyledProperty<int> PageSizeProperty =
        AvaloniaProperty.Register<CreatablePageableSearchableSelectionBox, int>(nameof(PageSize), defaultValue: 10);

    public static readonly StyledProperty<Func<string?, int, int, IReadOnlyList<int>?, CancellationToken, Task<PagedResultDto<SelectionItem>?>?>?> LoadPageAsyncProperty =
        AvaloniaProperty.Register<CreatablePageableSearchableSelectionBox, Func<string?, int, int, IReadOnlyList<int>?, CancellationToken, Task<PagedResultDto<SelectionItem>?>?>?>(nameof(LoadPageAsync));

    public static readonly StyledProperty<object?> AddNewCommandProperty =
        AvaloniaProperty.Register<CreatablePageableSearchableSelectionBox, object?>(nameof(AddNewCommand));

    public static readonly StyledProperty<bool> ShowAddNewProperty =
        AvaloniaProperty.Register<CreatablePageableSearchableSelectionBox, bool>(nameof(ShowAddNew));

    public static readonly StyledProperty<bool> ShowResultsProperty =
        AvaloniaProperty.Register<CreatablePageableSearchableSelectionBox, bool>(nameof(ShowResults));

    public static readonly StyledProperty<int> CurrentPageProperty =
        AvaloniaProperty.Register<CreatablePageableSearchableSelectionBox, int>(nameof(CurrentPage), defaultValue: 1);

    public static readonly StyledProperty<int> TotalCountProperty =
        AvaloniaProperty.Register<CreatablePageableSearchableSelectionBox, int>(nameof(TotalCount));

    public static readonly StyledProperty<string> CurrentPageInfoProperty =
        AvaloniaProperty.Register<CreatablePageableSearchableSelectionBox, string>(nameof(CurrentPageInfo), defaultValue: "当前页:1");

    public static readonly StyledProperty<string> TotalCountInfoProperty =
        AvaloniaProperty.Register<CreatablePageableSearchableSelectionBox, string>(nameof(TotalCountInfo), defaultValue: "共0条记录");

    #endregion

    #region Property Accessors

    public bool IsPopupOpen { get => GetValue(IsPopupOpenProperty); set => SetValue(IsPopupOpenProperty, value); }
    public SelectionItem? SelectedItem { get => GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }
    public string? Watermark { get => GetValue(WatermarkProperty); set => SetValue(WatermarkProperty, value); }
    public int PageSize { get => GetValue(PageSizeProperty); set => SetValue(PageSizeProperty, value); }

    public Func<string?, int, int, IReadOnlyList<int>?, CancellationToken, Task<PagedResultDto<SelectionItem>?>?>? LoadPageAsync
    {
        get => GetValue(LoadPageAsyncProperty);
        set => SetValue(LoadPageAsyncProperty, value);
    }

    public object? AddNewCommand { get => GetValue(AddNewCommandProperty); set => SetValue(AddNewCommandProperty, value); }
    public bool ShowAddNew { get => GetValue(ShowAddNewProperty); set => SetValue(ShowAddNewProperty, value); }
    public bool ShowResults { get => GetValue(ShowResultsProperty); set => SetValue(ShowResultsProperty, value); }
    public int CurrentPage { get => GetValue(CurrentPageProperty); set => SetValue(CurrentPageProperty, value); }
    public int TotalCount { get => GetValue(TotalCountProperty); set => SetValue(TotalCountProperty, value); }
    public string CurrentPageInfo { get => GetValue(CurrentPageInfoProperty); set => SetValue(CurrentPageInfoProperty, value); }
    public string TotalCountInfo { get => GetValue(TotalCountInfoProperty); set => SetValue(TotalCountInfoProperty, value); }

    #endregion

    /// <summary>当前页展示项，供模板 DataGrid 绑定。</summary>
    public ObservableCollection<SelectionItem> CurrentPageItems { get; } = new();

    #region Template Parts

    internal TextBox? PART_TextBox { get; private set; }
    internal Popup? PART_Popup { get; private set; }
    internal Border? PART_RootBorder { get; private set; }
    internal DataGrid? PART_DataGrid { get; private set; }
    internal Panel? PART_EmptyPanel { get; private set; }
    internal Button? PART_AddNewButton { get; private set; }

    #endregion

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
        _currentPageSub?.Dispose();

        PART_TextBox = e.NameScope.Find<TextBox>("PART_TextBox");
        PART_Popup = e.NameScope.Find<Popup>("PART_Popup");
        PART_RootBorder = e.NameScope.Find<Border>("PART_RootBorder");
        PART_DataGrid = e.NameScope.Find<DataGrid>("PART_DataGrid");
        PART_EmptyPanel = e.NameScope.Find<Panel>("PART_EmptyPanel");
        PART_AddNewButton = e.NameScope.Find<Button>("PART_AddNewButton");

        if (PART_RootBorder != null)
            PART_RootBorder.PointerPressed += OnRootPointerPressed;
        if (PART_TextBox != null)
        {
            PART_TextBox.TextChanged += OnTextBoxTextChanged;
            PART_TextBox.KeyDown += OnTextBoxKeyDown;
        }
        if (PART_DataGrid != null)
        {
            PART_DataGrid.SelectionChanged += OnDataGridSelectionChanged;
            PART_DataGrid.DoubleTapped += OnDataGridDoubleTapped;
        }
        if (PART_Popup != null)
            PART_Popup.Closed += OnPopupClosed;

        _isPopupOpenSub = this.GetObservable(IsPopupOpenProperty).Subscribe(OnIsPopupOpenChanged);
        _selectedItemSub = this.GetObservable(SelectedItemProperty).Subscribe(_ => UpdateTextBoxFromSelectedItem());
        _currentPageSub = this.GetObservable(CurrentPageProperty).Subscribe(OnCurrentPageChanged);

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
            _suppressPageChangeLoad = true;
            CurrentPage = 1;
            _suppressPageChangeLoad = false;
            _ = LoadPageAsyncInternalAsync();
            Dispatcher.UIThread.Post(() => PART_TextBox?.Focus(), DispatcherPriority.Loaded);
        }
        else
        {
            ResetSearchTextToSelectedItem();
            SyncTextBoxToSearchText();
        }
    }

    private void OnCurrentPageChanged(int newPage)
    {
        if (!_suppressPageChangeLoad && IsPopupOpen)
            _ = LoadPageAsyncInternalAsync();
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
                _suppressPageChangeLoad = true;
                CurrentPage = 1;
                _suppressPageChangeLoad = false;
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

    private void OnDataGridSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (PART_DataGrid?.SelectedItem is SelectionItem item)
        {
            SelectedItem = item;
            IsPopupOpen = false;
            Dispatcher.UIThread.Post(() => PART_TextBox?.Focus(), DispatcherPriority.Loaded);
        }
    }

    private void OnDataGridDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (PART_DataGrid?.SelectedItem is SelectionItem item)
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

    private void UpdatePageInfo()
    {
        ShowResults = CurrentPageItems.Count > 0;
        CurrentPageInfo = $"当前页:{CurrentPage}";
        TotalCountInfo = $"共{TotalCount}条记录";
    }

    private async Task LoadPageAsyncInternalAsync()
    {
        var load = LoadPageAsync;
        if (load == null) return;

        CurrentPageItems.Clear();
        ShowAddNew = false;

        try
        {
            var selectedIds = GetSelectedIds();
            var result = await load(_searchText, CurrentPage, PageSize, selectedIds, CancellationToken.None)
                .ConfigureAwait(true);
            if (result is not { } r) return;

            TotalCount = (int)r.TotalCount;
            foreach (var item in r.Items ?? Array.Empty<SelectionItem>())
                CurrentPageItems.Add(item);

            if (CurrentPageItems.Count == 0)
                ShowAddNew = true;
        }
        catch
        {
            ShowAddNew = true;
        }
        finally
        {
            UpdatePageInfo();
        }
    }
}
