using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Threading;
using MaterialClient.Common.Models;
using Volo.Abp.Application.Dtos;

namespace MaterialClient.Views;

/// <summary>
/// 可创建、可分页、可搜索的选择控件。单一 TextBox 作为输入/展示面，内嵌 Popup 列表与分页。
/// </summary>
public partial class PageableSearchableSelectionBox : TemplatedControl
{
    public static readonly StyledProperty<SelectionItem?> SelectedItemProperty =
        AvaloniaProperty.Register<PageableSearchableSelectionBox, SelectionItem?>(
            nameof(SelectedItem), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<string?> WatermarkProperty =
        AvaloniaProperty.Register<PageableSearchableSelectionBox, string?>(
            nameof(Watermark), defaultValue: "请选择");

    public static readonly StyledProperty<int> PageSizeProperty =
        AvaloniaProperty.Register<PageableSearchableSelectionBox, int>(
            nameof(PageSize), defaultValue: 10);

    public static readonly StyledProperty<bool> IsPopupOpenProperty =
        AvaloniaProperty.Register<PageableSearchableSelectionBox, bool>(
            nameof(IsPopupOpen), defaultBindingMode: BindingMode.TwoWay, defaultValue: false);

    public static readonly StyledProperty<double?> PopupWidthProperty =
        AvaloniaProperty.Register<PageableSearchableSelectionBox, double?>(
            nameof(PopupWidth), defaultValue: 400);

    public static readonly StyledProperty<bool> AllowCreateNewProperty =
        AvaloniaProperty.Register<PageableSearchableSelectionBox, bool>(
            nameof(AllowCreateNew), defaultValue: true);

    public static readonly StyledProperty<string> AddNewButtonTextProperty =
        AvaloniaProperty.Register<PageableSearchableSelectionBox, string>(
            nameof(AddNewButtonText), defaultValue: "新增");

    public static readonly StyledProperty<ICommand?> AddNewCommandProperty =
        AvaloniaProperty.Register<PageableSearchableSelectionBox, ICommand?>(
            nameof(AddNewCommand));

    public static readonly StyledProperty<int> LoadingDelayMsProperty =
        AvaloniaProperty.Register<PageableSearchableSelectionBox, int>(
            nameof(LoadingDelayMs), defaultValue: 300);

    public static readonly StyledProperty<string?> SearchTextProperty =
        AvaloniaProperty.Register<PageableSearchableSelectionBox, string?>(
            nameof(SearchText), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<IEnumerable<SelectionItem>?> PagedItemsProperty =
        AvaloniaProperty.Register<PageableSearchableSelectionBox, IEnumerable<SelectionItem>?>(
            nameof(PagedItems), defaultValue: Array.Empty<SelectionItem>());

    public static readonly StyledProperty<int> CurrentPageProperty =
        AvaloniaProperty.Register<PageableSearchableSelectionBox, int>(
            nameof(CurrentPage), defaultValue: 1);

    public static readonly StyledProperty<int> TotalCountProperty =
        AvaloniaProperty.Register<PageableSearchableSelectionBox, int>(
            nameof(TotalCount), defaultValue: 0);

    public static readonly StyledProperty<int> TotalPagesProperty =
        AvaloniaProperty.Register<PageableSearchableSelectionBox, int>(
            nameof(TotalPages), defaultValue: 1);

    public static readonly StyledProperty<bool> ShowAddNewButtonProperty =
        AvaloniaProperty.Register<PageableSearchableSelectionBox, bool>(
            nameof(ShowAddNewButton), defaultValue: false);

    public static readonly StyledProperty<ICommand?> PageChangeCommandProperty =
        AvaloniaProperty.Register<PageableSearchableSelectionBox, ICommand?>(
            nameof(PageChangeCommand));

    public static readonly StyledProperty<ICommand?> AddNewItemCommandProperty =
        AvaloniaProperty.Register<PageableSearchableSelectionBox, ICommand?>(
            nameof(AddNewItemCommand));

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

    public bool IsPopupOpen
    {
        get => GetValue(IsPopupOpenProperty);
        set => SetValue(IsPopupOpenProperty, value);
    }

    public double? PopupWidth
    {
        get => GetValue(PopupWidthProperty);
        set => SetValue(PopupWidthProperty, value);
    }

    public bool AllowCreateNew
    {
        get => GetValue(AllowCreateNewProperty);
        set => SetValue(AllowCreateNewProperty, value);
    }

    public string AddNewButtonText
    {
        get => GetValue(AddNewButtonTextProperty);
        set => SetValue(AddNewButtonTextProperty, value);
    }

    public ICommand? AddNewCommand
    {
        get => GetValue(AddNewCommandProperty);
        set => SetValue(AddNewCommandProperty, value);
    }

    public int LoadingDelayMs
    {
        get => GetValue(LoadingDelayMsProperty);
        set => SetValue(LoadingDelayMsProperty, value);
    }

    public string? SearchText
    {
        get => GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    public IEnumerable<SelectionItem>? PagedItems
    {
        get => GetValue(PagedItemsProperty);
        set => SetValue(PagedItemsProperty, value);
    }

    public int CurrentPage
    {
        get => GetValue(CurrentPageProperty);
        set => SetValue(CurrentPageProperty, value);
    }

    public int TotalCount
    {
        get => GetValue(TotalCountProperty);
        set => SetValue(TotalCountProperty, value);
    }

    public int TotalPages
    {
        get => GetValue(TotalPagesProperty);
        set => SetValue(TotalPagesProperty, value);
    }

    public bool ShowAddNewButton
    {
        get => GetValue(ShowAddNewButtonProperty);
        set => SetValue(ShowAddNewButtonProperty, value);
    }

    public ICommand? PageChangeCommand
    {
        get => GetValue(PageChangeCommandProperty);
        set => SetValue(PageChangeCommandProperty, value);
    }

    public ICommand? AddNewItemCommand
    {
        get => GetValue(AddNewItemCommandProperty);
        set => SetValue(AddNewItemCommandProperty, value);
    }

    /// <summary>
    /// 分页加载委托。签名: (searchText, page, pageSize, selectedIds, cancellationToken) => Task(PagedResultDto(SelectionItem))
    /// </summary>
    public Func<string?, int, int, IReadOnlyList<int>?, CancellationToken, Task<PagedResultDto<SelectionItem>>>? LoadPageAsync { get; set; }

    private TextBox? _partTextBox;
    private Popup? _partPopup;
    private DataGrid? _partItemsList;
    private IDisposable? _searchSubscription;
    private CancellationTokenSource? _loadCts;
    private CancellationTokenSource? _debounceCts;

    public PageableSearchableSelectionBox()
    {
        InitializeComponent();
        Focusable = true;
        var pageCmd = new PageChangeCommandImpl(this);
        SetValue(PageChangeCommandProperty, pageCmd);
        SetValue(AddNewItemCommandProperty, new AddNewItemCommandImpl(this));
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _searchSubscription?.Dispose();
        _searchSubscription = this.GetObservable(SearchTextProperty)
            .Throttle(TimeSpan.FromMilliseconds(LoadingDelayMs))
            .Subscribe(_ => LoadDataAsync());

        _partTextBox = e.NameScope.Find<TextBox>("PART_TextBox");
        _partPopup = e.NameScope.Find<Popup>("PART_Popup");
        _partItemsList = e.NameScope.Find<DataGrid>("PART_ItemsList");

        if (_partTextBox != null)
        {
            _partTextBox.GotFocus -= OnTextBoxGotFocus;
            _partTextBox.GotFocus += OnTextBoxGotFocus;
            _partTextBox.KeyDown -= OnTextBoxKeyDown;
            _partTextBox.KeyDown += OnTextBoxKeyDown;
        }

        if (_partPopup != null)
        {
            _partPopup.Opened -= OnPopupOpened;
            _partPopup.Opened += OnPopupOpened;
            _partPopup.Closed -= OnPopupClosed;
            _partPopup.Closed += OnPopupClosed;
        }

        if (_partItemsList != null)
        {
            _partItemsList.SelectionChanged -= OnDataGridSelectionChanged;
            _partItemsList.SelectionChanged += OnDataGridSelectionChanged;
            _partItemsList.KeyDown -= OnDataGridKeyDown;
            _partItemsList.KeyDown += OnDataGridKeyDown;
        }
    }

    private void OnTextBoxGotFocus(object? sender, GotFocusEventArgs e)
    {
        if (!IsPopupOpen)
        {
            SearchText = SelectedItem?.Name ?? "";
            IsPopupOpen = true;
        }
    }

    private void OnTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            IsPopupOpen = false;
            SearchText = SelectedItem?.Name ?? "";
            e.Handled = true;
        }
        else if (IsPopupOpen && (e.Key == Key.Down || e.Key == Key.Enter))
        {
            _partItemsList?.Focus();
            e.Handled = true;
        }
    }

    private void OnPopupOpened(object? sender, EventArgs e)
    {
        SearchText = SelectedItem?.Name ?? "";
        CurrentPage = 1;
        _ = LoadDataAsync();
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        SearchText = SelectedItem?.Name ?? "";
        Dispatcher.UIThread.Post(() => _partTextBox?.Focus(), DispatcherPriority.Loaded);
    }

    private void OnDataGridSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_partItemsList?.SelectedItem is SelectionItem item)
        {
            SelectedItem = item;
            IsPopupOpen = false;
            SearchText = item.Name;
        }
    }

    private void OnDataGridKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _partItemsList?.SelectedItem is SelectionItem item)
        {
            SelectedItem = item;
            IsPopupOpen = false;
            SearchText = item.Name;
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            IsPopupOpen = false;
            SearchText = SelectedItem?.Name ?? "";
            _partTextBox?.Focus();
            e.Handled = true;
        }
    }

    private IReadOnlyList<int>? GetSelectedIds()
    {
        var sel = SelectedItem;
        if (sel == null) return null;
        return new[] { sel.Id };
    }

    private async Task LoadDataAsync()
    {
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        var searchText = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim();
        var selectedIds = GetSelectedIds();
        var load = LoadPageAsync;
        if (load == null)
        {
            SetEmptyResult();
            return;
        }

        try
        {
            var result = await load(searchText, CurrentPage, PageSize, selectedIds, ct);
            if (ct.IsCancellationRequested) return;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                PagedItems = result.Items ?? Array.Empty<SelectionItem>();
                TotalCount = (int)result.TotalCount;
                TotalPages = TotalCount > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 1;
                ShowAddNewButton = TotalCount == 0 && AllowCreateNew && AddNewCommand != null;
                RestoreSelectedItemIfInCurrentPage();
            });
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        catch (Exception)
        {
            await Dispatcher.UIThread.InvokeAsync(SetEmptyResult);
        }
    }

    private void SetEmptyResult()
    {
        PagedItems = Array.Empty<SelectionItem>();
        TotalCount = 0;
        TotalPages = 1;
        ShowAddNewButton = AllowCreateNew && AddNewCommand != null;
    }

    private void RestoreSelectedItemIfInCurrentPage()
    {
        var sel = SelectedItem;
        if (sel == null || PagedItems == null) return;
        var list = PagedItems.ToList();
        var found = list.FirstOrDefault(x => x.Id == sel.Id);
        if (found != null && _partItemsList != null)
        {
            _partItemsList.SelectedItem = found;
        }
    }

    private sealed class PageChangeCommandImpl : ICommand
    {
        private readonly PageableSearchableSelectionBox _box;

        public PageChangeCommandImpl(PageableSearchableSelectionBox box) => _box = box;

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
            _ = _box.LoadDataAsync();
        }

        public event EventHandler? CanExecuteChanged;
    }

    private sealed class AddNewItemCommandImpl : ICommand
    {
        private readonly PageableSearchableSelectionBox _box;

        public AddNewItemCommandImpl(PageableSearchableSelectionBox box) => _box = box;

        public bool CanExecute(object? parameter) => _box.AddNewCommand?.CanExecute(_box.SearchText) ?? false;

        public void Execute(object? parameter)
        {
            // 将当前搜索文本作为“新增”名称传给外部命令
            _box.AddNewCommand?.Execute(_box.SearchText);
        }

        public event EventHandler? CanExecuteChanged;
    }
}
