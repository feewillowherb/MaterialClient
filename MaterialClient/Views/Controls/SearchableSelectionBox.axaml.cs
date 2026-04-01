using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using MaterialClient.Common.Models;
using Volo.Abp.Application.Dtos;

namespace MaterialClient.Views.Controls;

public partial class SearchableSelectionBox : UserControl
{
    #region StyledProperties

    public static readonly StyledProperty<SelectionItem?> SelectedItemProperty =
        AvaloniaProperty.Register<SearchableSelectionBox, SelectionItem?>(nameof(SelectedItem));

    public static readonly StyledProperty<Func<string?, int, int, IReadOnlyList<int>?, Task<PagedResultDto<SelectionItem>>>?> LoadPageAsyncProperty =
        AvaloniaProperty.Register<SearchableSelectionBox, Func<string?, int, int, IReadOnlyList<int>?, Task<PagedResultDto<SelectionItem>>>?>(nameof(LoadPageAsync));

    public static readonly StyledProperty<Func<string, Task<SelectionItem?>>?> CreateNewAsyncProperty =
        AvaloniaProperty.Register<SearchableSelectionBox, Func<string, Task<SelectionItem?>>?>(nameof(CreateNewAsync));

    public static readonly StyledProperty<string?> WatermarkProperty =
        AvaloniaProperty.Register<SearchableSelectionBox, string?>(nameof(Watermark), "请选择");

    public static readonly StyledProperty<bool> AllowCreateNewProperty =
        AvaloniaProperty.Register<SearchableSelectionBox, bool>(nameof(AllowCreateNew), true);

    public static readonly StyledProperty<int> PageSizeProperty =
        AvaloniaProperty.Register<SearchableSelectionBox, int>(nameof(PageSize), 4);

    public static readonly StyledProperty<double> PopupWidthProperty =
        AvaloniaProperty.Register<SearchableSelectionBox, double>(nameof(PopupWidth), 400);

    public static readonly StyledProperty<bool> IsDropdownOpenProperty =
        AvaloniaProperty.Register<SearchableSelectionBox, bool>(nameof(IsDropdownOpen));

    public static readonly StyledProperty<int> CurrentPageProperty =
        AvaloniaProperty.Register<SearchableSelectionBox, int>(nameof(CurrentPage), 1);

    public static readonly StyledProperty<int> TotalCountProperty =
        AvaloniaProperty.Register<SearchableSelectionBox, int>(nameof(TotalCount));

    #endregion

    private bool _isInitializing;
    private string _searchText = string.Empty;
    private SelectionItem? _selectedItemBeforeOpen;

    public SelectionItem? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public Func<string?, int, int, IReadOnlyList<int>?, Task<PagedResultDto<SelectionItem>>>? LoadPageAsync
    {
        get => GetValue(LoadPageAsyncProperty);
        set => SetValue(LoadPageAsyncProperty, value);
    }

    public Func<string, Task<SelectionItem?>>? CreateNewAsync
    {
        get => GetValue(CreateNewAsyncProperty);
        set => SetValue(CreateNewAsyncProperty, value);
    }

    public string? Watermark
    {
        get => GetValue(WatermarkProperty);
        set => SetValue(WatermarkProperty, value);
    }

    public bool AllowCreateNew
    {
        get => GetValue(AllowCreateNewProperty);
        set => SetValue(AllowCreateNewProperty, value);
    }

    public int PageSize
    {
        get => GetValue(PageSizeProperty);
        set => SetValue(PageSizeProperty, value);
    }

    public double PopupWidth
    {
        get => GetValue(PopupWidthProperty);
        set => SetValue(PopupWidthProperty, value);
    }

    public bool IsDropdownOpen
    {
        get => GetValue(IsDropdownOpenProperty);
        set => SetValue(IsDropdownOpenProperty, value);
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

    private ObservableCollection<SelectionItem> PagedItems { get; } = new();

    public SearchableSelectionBox()
    {
        InitializeComponent();
        Focusable = true;

        this.GetObservable(IsDropdownOpenProperty).Subscribe(OnIsDropdownOpenChanged);
        this.GetObservable(SelectedItemProperty).Subscribe(_ => UpdateDisplayText());

        // Page change → reload data
        this.GetObservable(CurrentPageProperty)
            .DistinctUntilChanged()
            .Subscribe(_p =>
            {
                if (IsDropdownOpen)
                    _ = LoadDataAsync();
            });

        // Debounced search: 300ms throttle
        SearchTextBox.GetObservable(TextBox.TextProperty)
            .Throttle(TimeSpan.FromMilliseconds(300))
            .ObserveOn(AvaloniaScheduler.Instance)
            .Subscribe(text =>
            {
                _searchText = text ?? string.Empty;
                CurrentPage = 1;
                // If already on page 1, CurrentPage setter won't trigger reload
                if (CurrentPage == 1)
                    _ = LoadDataAsync();
            });
    }

    #region Display

    private void UpdateDisplayText()
    {
        if (DisplayTextBlock == null) return;
        if (_isInitializing) return;

        if (SelectedItem != null)
        {
            DisplayTextBlock.Text = SelectedItem.Name;
            DisplayTextBlock.Foreground = new SolidColorBrush(Color.Parse("#333333"));
        }
        else
        {
            DisplayTextBlock.Text = Watermark ?? "请选择";
            DisplayTextBlock.Foreground = new SolidColorBrush(Color.Parse("#999999"));
        }
    }

    #endregion

    #region Popup Open/Close

    private void OnIsDropdownOpenChanged(bool isOpen)
    {
        if (DisplayTextBlock == null || SearchTextBox == null) return;

        if (isOpen)
        {
            _selectedItemBeforeOpen = SelectedItem;
            DisplayTextBlock.IsVisible = false;
            SearchTextBox.IsVisible = true;
            SearchTextBox.Text = string.Empty;
            _searchText = string.Empty;
            CurrentPage = 1;

            Dispatcher.UIThread.Post(() => SearchTextBox.Focus(), DispatcherPriority.Loaded);
            _ = LoadDataAsync();
        }
        else
        {
            SearchTextBox.IsVisible = false;
            DisplayTextBlock.IsVisible = true;
            UpdateDisplayText();
        }
    }

    private void ClosePopup()
    {
        IsDropdownOpen = false;
    }

    #endregion

    #region Data Loading

    private async Task LoadDataAsync()
    {
        var loadFunc = LoadPageAsync;
        if (loadFunc == null)
        {
            PagedItems.Clear();
            UpdateListVisualState();
            return;
        }

        try
        {
            IReadOnlyList<int>? selectedIds = null;
            if (SelectedItem != null)
                selectedIds = new List<int> { SelectedItem.Id };

            var search = string.IsNullOrWhiteSpace(_searchText) ? null : _searchText.Trim();
            var result = await loadFunc(search, CurrentPage, PageSize, selectedIds);

            TotalCount = (int)result.TotalCount;

            _isInitializing = true;
            try
            {
                PagedItems.Clear();
                foreach (var item in result.Items)
                    PagedItems.Add(item);

                ItemsDataGrid.ItemsSource = null;
                ItemsDataGrid.ItemsSource = PagedItems;

                // Restore selection
                if (SelectedItem != null)
                {
                    var match = PagedItems.FirstOrDefault(x => x.Id == SelectedItem.Id);
                    if (match != null)
                        ItemsDataGrid.SelectedItem = match;
                }
            }
            finally
            {
                _isInitializing = false;
            }

            UpdateListVisualState();
        }
        catch
        {
            PagedItems.Clear();
            UpdateListVisualState();
        }
    }

    private void UpdateListVisualState()
    {
        var hasResults = PagedItems.Count > 0;
        var searchTrimmed = _searchText?.Trim() ?? string.Empty;
        var canCreateNew = AllowCreateNew && CreateNewAsync != null && !string.IsNullOrEmpty(searchTrimmed);
        var searchTextExistsInResults = hasResults && PagedItems.Any(x =>
            string.Equals(x.Name, searchTrimmed, StringComparison.OrdinalIgnoreCase));

        ItemsDataGrid.IsVisible = hasResults;

        if (!hasResults && canCreateNew)
        {
            NoResultsPanel.IsVisible = true;
            AddNewBelowListButton.IsVisible = false;
        }
        else
        {
            NoResultsPanel.IsVisible = false;
            AddNewBelowListButton.IsVisible = hasResults && canCreateNew && !searchTextExistsInResults;
        }
    }

    #endregion

    #region Event Handlers

    private void OnRootPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsDropdownOpen)
        {
            IsDropdownOpen = true;
            e.Handled = true;
        }
    }

    private void OnItemPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control) return;
        if (control.DataContext is not SelectionItem item) return;

        ConfirmSelection(item);
        e.Handled = true;
    }

    private void ConfirmSelection(SelectionItem item)
    {
        SelectedItem = item;
        ClosePopup();
    }

    private async void OnAddNewClick(object? sender, RoutedEventArgs e)
    {
        var createFunc = CreateNewAsync;
        if (createFunc == null) return;

        var name = _searchText?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name)) return;

        try
        {
            var newItem = await createFunc(name);
            if (newItem != null)
            {
                SelectedItem = newItem;
                ClosePopup();
            }
            // null => keep popup open
        }
        catch
        {
            // keep popup open on error
        }
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            SelectedItem = _selectedItemBeforeOpen;
            ClosePopup();
            e.Handled = true;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateDisplayText();
    }

    #endregion
}
