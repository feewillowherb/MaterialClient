using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
/// 可创建、可分页、可搜索的单一选择控件。
/// 公共 API: SelectedId (int?), LoadPageAsync (Func), CreateNewAsync (Func?), Watermark, PageSize。
/// SelectionItem 为内部数据载体，不暴露给 ViewModel。
/// </summary>
public class CreatablePageableSearchableSelectionBox : TemplatedControl
{
    private const int DebounceMs = 300;
    private const int SuppressOpenDelayMs = 200;

    private string _searchText = string.Empty;
    private string _selectedDisplayName = string.Empty;
    private CancellationTokenSource? _debounceCts;
    private bool _suppressPageChangeLoad;
    private bool _suppressTextChanged;
    private bool _suppressNextOpen;
    private bool _suppressSelectedIdReload;

    private IDisposable? _isPopupOpenSub;
    private IDisposable? _selectedIdSub;
    private IDisposable? _currentPageSub;

    #region Styled Properties

    public static readonly StyledProperty<bool> IsPopupOpenProperty =
        AvaloniaProperty.Register<CreatablePageableSearchableSelectionBox, bool>(nameof(IsPopupOpen));

    public static readonly StyledProperty<int?> SelectedIdProperty =
        AvaloniaProperty.Register<CreatablePageableSearchableSelectionBox, int?>(nameof(SelectedId));

    public static readonly StyledProperty<string?> WatermarkProperty =
        AvaloniaProperty.Register<CreatablePageableSearchableSelectionBox, string?>(nameof(Watermark), defaultValue: "请选择");

    public static readonly StyledProperty<int> PageSizeProperty =
        AvaloniaProperty.Register<CreatablePageableSearchableSelectionBox, int>(nameof(PageSize), defaultValue: 10);

    public static readonly StyledProperty<Func<string?, int, int, IReadOnlyList<int>?, CancellationToken, Task<PagedResultDto<SelectionItem>?>?>?> LoadPageAsyncProperty =
        AvaloniaProperty.Register<CreatablePageableSearchableSelectionBox, Func<string?, int, int, IReadOnlyList<int>?, CancellationToken, Task<PagedResultDto<SelectionItem>?>?>?>(nameof(LoadPageAsync));

    public static readonly StyledProperty<Func<string, CancellationToken, Task<SelectionItem?>>?> CreateNewAsyncProperty =
        AvaloniaProperty.Register<CreatablePageableSearchableSelectionBox, Func<string, CancellationToken, Task<SelectionItem?>>?>(nameof(CreateNewAsync));

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
    public int? SelectedId { get => GetValue(SelectedIdProperty); set => SetValue(SelectedIdProperty, value); }
    public string? Watermark { get => GetValue(WatermarkProperty); set => SetValue(WatermarkProperty, value); }
    public int PageSize { get => GetValue(PageSizeProperty); set => SetValue(PageSizeProperty, value); }

    public Func<string?, int, int, IReadOnlyList<int>?, CancellationToken, Task<PagedResultDto<SelectionItem>?>?>? LoadPageAsync
    {
        get => GetValue(LoadPageAsyncProperty);
        set => SetValue(LoadPageAsyncProperty, value);
    }

    public Func<string, CancellationToken, Task<SelectionItem?>>? CreateNewAsync
    {
        get => GetValue(CreateNewAsyncProperty);
        set => SetValue(CreateNewAsyncProperty, value);
    }

    public bool ShowAddNew { get => GetValue(ShowAddNewProperty); set => SetValue(ShowAddNewProperty, value); }
    public bool ShowResults { get => GetValue(ShowResultsProperty); set => SetValue(ShowResultsProperty, value); }
    public int CurrentPage { get => GetValue(CurrentPageProperty); set => SetValue(CurrentPageProperty, value); }
    public int TotalCount { get => GetValue(TotalCountProperty); set => SetValue(TotalCountProperty, value); }
    public string CurrentPageInfo { get => GetValue(CurrentPageInfoProperty); set => SetValue(CurrentPageInfoProperty, value); }
    public string TotalCountInfo { get => GetValue(TotalCountInfoProperty); set => SetValue(TotalCountInfoProperty, value); }

    #endregion

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
        Focusable = false;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _isPopupOpenSub?.Dispose();
        _selectedIdSub?.Dispose();
        _currentPageSub?.Dispose();

        PART_TextBox = e.NameScope.Find<TextBox>("PART_TextBox");
        PART_Popup = e.NameScope.Find<Popup>("PART_Popup");
        PART_RootBorder = e.NameScope.Find<Border>("PART_RootBorder");
        PART_DataGrid = e.NameScope.Find<DataGrid>("PART_DataGrid");
        PART_EmptyPanel = e.NameScope.Find<Panel>("PART_EmptyPanel");
        PART_AddNewButton = e.NameScope.Find<Button>("PART_AddNewButton");

        AddHandler(PointerPressedEvent, OnRootPointerPressed, RoutingStrategies.Tunnel, true);
        if (PART_TextBox != null)
        {
            PART_TextBox.IsReadOnly = true;
            PART_TextBox.Focusable = false;
            PART_TextBox.IsHitTestVisible = false;
            PART_TextBox.TextChanged += OnTextBoxTextChanged;
            PART_TextBox.KeyDown += OnTextBoxKeyDown;
        }
        if (PART_DataGrid != null)
        {
            PART_DataGrid.SelectionChanged += OnDataGridSelectionChanged;
            PART_DataGrid.DoubleTapped += OnDataGridDoubleTapped;
        }
        if (PART_AddNewButton != null)
            PART_AddNewButton.Click += OnAddNewButtonClick;
        if (PART_Popup != null)
            PART_Popup.Closed += OnPopupClosed;

        _isPopupOpenSub = this.GetObservable(IsPopupOpenProperty).Subscribe(OnIsPopupOpenChanged);
        _selectedIdSub = this.GetObservable(SelectedIdProperty).Subscribe(OnSelectedIdChanged);
        _currentPageSub = this.GetObservable(CurrentPageProperty).Subscribe(OnCurrentPageChanged);

        UpdateDisplayFromSelectedId();
    }

    private void OnRootPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsPopupOpen && !_suppressNextOpen)
            Dispatcher.UIThread.Post(() =>
            {
                if (!IsPopupOpen && !_suppressNextOpen)
                {
                    DiagLog("OnRootPointerPressed.Post → IsPopupOpen = true");
                    IsPopupOpen = true;
                }
            });
    }

    private void OnIsPopupOpenChanged(bool isOpen)
    {
        DiagLog($"OnIsPopupOpenChanged({isOpen})\n{new StackTrace(true)}");
        if (isOpen)
        {
            if (PART_TextBox != null)
            {
                PART_TextBox.Focusable = true;
                PART_TextBox.IsHitTestVisible = true;
                PART_TextBox.IsReadOnly = false;
            }
            _searchText = _selectedDisplayName;
            SyncTextBoxToSearchText();
            _suppressPageChangeLoad = true;
            CurrentPage = 1;
            _suppressPageChangeLoad = false;
            _ = LoadPageAsyncInternalAsync();
            Dispatcher.UIThread.Post(() => PART_TextBox?.Focus(), DispatcherPriority.Loaded);
        }
        else
        {
            _debounceCts?.Cancel();
            _suppressNextOpen = true;
            DispatcherTimer.RunOnce(() => _suppressNextOpen = false,
                TimeSpan.FromMilliseconds(SuppressOpenDelayMs));

            if (PART_TextBox != null)
            {
                PART_TextBox.IsReadOnly = true;
                PART_TextBox.Focusable = false;
                PART_TextBox.IsHitTestVisible = false;
            }
            _searchText = _selectedDisplayName;
            SyncTextBoxToDisplayText();
            _debounceCts?.Cancel();
            _debounceCts = null;
        }
    }

    private void OnSelectedIdChanged(int? newId)
    {
        if (_suppressSelectedIdReload) return;
        ResolveDisplayNameFromItems();
        if (!IsPopupOpen)
            UpdateDisplayFromSelectedId();
    }

    private void OnCurrentPageChanged(int newPage)
    {
        if (!_suppressPageChangeLoad && IsPopupOpen)
            _ = LoadPageAsyncInternalAsync();
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        DiagLog($"OnPopupClosed: IsPopupOpen={IsPopupOpen}");
        _searchText = _selectedDisplayName;
        SyncTextBoxToDisplayText();
    }

    private void OnTextBoxTextChanged(object? sender, TextChangedEventArgs e)
    {
        DiagLog($"OnTextBoxTextChanged: suppressed={_suppressTextChanged}, text='{PART_TextBox?.Text}'");
        if (PART_TextBox == null || _suppressTextChanged) return;
        _searchText = PART_TextBox.Text ?? string.Empty;
        DiagLog($"OnTextBoxTextChanged: creating debounce for '{_searchText}'");
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var ct = _debounceCts.Token;
        _ = Task.Run(async () =>
        {
            await Task.Delay(DebounceMs, ct).ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                DiagLog($"debounce callback: IsPopupOpen={IsPopupOpen}");
                if (!IsPopupOpen) return;
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
        DiagLog($"OnDataGridSelectionChanged: SelectedItem={PART_DataGrid?.SelectedItem}, type={PART_DataGrid?.SelectedItem?.GetType().Name}");
        if (PART_DataGrid?.SelectedItem is SelectionItem item)
            AcceptSelection(item);
    }

    private void OnDataGridDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (PART_DataGrid?.SelectedItem is SelectionItem item)
            AcceptSelection(item);
    }

    private void AcceptSelection(SelectionItem item)
    {
        DiagLog($"AcceptSelection: Id={item.Id}, Name='{item.Name}', current IsPopupOpen={IsPopupOpen}");
        _selectedDisplayName = item.Name;
        _suppressSelectedIdReload = true;
        SelectedId = item.Id;
        _suppressSelectedIdReload = false;
        DiagLog("AcceptSelection: setting IsPopupOpen=false");
        IsPopupOpen = false;
        DiagLog($"AcceptSelection: after IsPopupOpen=false, actual value={IsPopupOpen}");
        ClearFocusFromControl();
    }

    private async void OnAddNewButtonClick(object? sender, RoutedEventArgs e)
    {
        var create = CreateNewAsync;
        if (create == null) return;

        try
        {
            var result = await create(_searchText, CancellationToken.None);
            if (result == null) return;

            _selectedDisplayName = result.Name;
            _suppressSelectedIdReload = true;
            SelectedId = result.Id;
            _suppressSelectedIdReload = false;
            IsPopupOpen = false;
            ClearFocusFromControl();
        }
        catch
        {
            // Creation failed; leave popup open for retry
        }
    }

    private void ResolveDisplayNameFromItems()
    {
        var id = SelectedId;
        if (id == null)
        {
            _selectedDisplayName = string.Empty;
            return;
        }
        var match = CurrentPageItems.FirstOrDefault(i => i.Id == id.Value);
        if (match != null)
            _selectedDisplayName = match.Name;
    }

    private void SyncTextBoxToSearchText()
    {
        if (PART_TextBox != null)
        {
            _suppressTextChanged = true;
            PART_TextBox.Text = _searchText;
            _suppressTextChanged = false;
        }
    }

    private void SyncTextBoxToDisplayText()
    {
        if (PART_TextBox != null)
        {
            _suppressTextChanged = true;
            PART_TextBox.Text = string.IsNullOrEmpty(_selectedDisplayName)
                ? (Watermark ?? string.Empty)
                : _selectedDisplayName;
            _suppressTextChanged = false;
        }
    }

    private void UpdateDisplayFromSelectedId()
    {
        if (!IsPopupOpen)
        {
            ResolveDisplayNameFromItems();
            SyncTextBoxToDisplayText();
        }
    }

    [Conditional("DEBUG")]
    private static void DiagLog(string msg)
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] {msg}";
        try { File.AppendAllText("popup-diag.log", line + Environment.NewLine); } catch { }
    }

    private void ClearFocusFromControl()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var topLevel = TopLevel.GetTopLevel(this);
            topLevel?.Focus();
        }, DispatcherPriority.Loaded);
    }

    private IReadOnlyList<int>? GetSelectedIds()
    {
        var id = SelectedId;
        if (id == null) return null;
        return new[] { id.Value };
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

            ResolveDisplayNameFromItems();

            if (CurrentPageItems.Count == 0)
                ShowAddNew = CreateNewAsync != null;
        }
        catch
        {
            ShowAddNew = CreateNewAsync != null;
        }
        finally
        {
            UpdatePageInfo();
        }
    }
}
