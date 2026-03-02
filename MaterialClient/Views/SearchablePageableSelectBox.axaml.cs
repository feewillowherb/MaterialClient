using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Volo.Abp.Application.Dtos;

namespace MaterialClient.Views;

public partial class SearchablePageableSelectBox : TemplatedControl
{
    public static readonly StyledProperty<object?> SelectedItemProperty =
        AvaloniaProperty.Register<SearchablePageableSelectBox, object?>(nameof(SelectedItem), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<string?> DisplayMemberPathProperty =
        AvaloniaProperty.Register<SearchablePageableSelectBox, string?>(nameof(DisplayMemberPath));

    public static readonly StyledProperty<Func<object?, int>?> GetItemIdProperty =
        AvaloniaProperty.Register<SearchablePageableSelectBox, Func<object?, int>?>(nameof(GetItemId));

    public static readonly StyledProperty<string?> WatermarkProperty =
        AvaloniaProperty.Register<SearchablePageableSelectBox, string?>(nameof(Watermark));

    public static readonly StyledProperty<int> PageSizeProperty =
        AvaloniaProperty.Register<SearchablePageableSelectBox, int>(nameof(PageSize), defaultValue: 10);

    public static readonly StyledProperty<Func<string, int, int, Task<PagedResultDto<object>>>?> LoadPageAsyncProperty =
        AvaloniaProperty.Register<SearchablePageableSelectBox, Func<string, int, int, Task<PagedResultDto<object>>>?>(nameof(LoadPageAsync));

    public static readonly StyledProperty<IReactiveCommand?> AddNewCommandProperty =
        AvaloniaProperty.Register<SearchablePageableSelectBox, IReactiveCommand?>(nameof(AddNewCommand));

    public static readonly StyledProperty<bool> IsPopupOpenProperty =
        AvaloniaProperty.Register<SearchablePageableSelectBox, bool>(nameof(IsPopupOpen), defaultValue: false);

    public static readonly StyledProperty<ObservableCollection<object>> ItemsProperty =
        AvaloniaProperty.Register<SearchablePageableSelectBox, ObservableCollection<object>>(nameof(Items));

    public static readonly StyledProperty<int> CurrentPageProperty =
        AvaloniaProperty.Register<SearchablePageableSelectBox, int>(nameof(CurrentPage), defaultValue: 1);

    public static readonly StyledProperty<int> TotalCountProperty =
        AvaloniaProperty.Register<SearchablePageableSelectBox, int>(nameof(TotalCount), defaultValue: 0);

    public static readonly StyledProperty<string> CurrentPageInfoProperty =
        AvaloniaProperty.Register<SearchablePageableSelectBox, string>(nameof(CurrentPageInfo), defaultValue: "当前页: 1");

    public static readonly StyledProperty<string> TotalCountInfoProperty =
        AvaloniaProperty.Register<SearchablePageableSelectBox, string>(nameof(TotalCountInfo), defaultValue: "共0条记录");

    public static readonly StyledProperty<bool> ShowAddNewProperty =
        AvaloniaProperty.Register<SearchablePageableSelectBox, bool>(nameof(ShowAddNew), defaultValue: false);

    public static readonly StyledProperty<IReactiveCommand> PageChangeCommandProperty =
        AvaloniaProperty.Register<SearchablePageableSelectBox, IReactiveCommand>(nameof(PageChangeCommand));

    // Read-only direct property for IsLoading
    public static readonly DirectProperty<SearchablePageableSelectBox, bool> IsLoadingProperty =
        AvaloniaProperty.RegisterDirect<SearchablePageableSelectBox, bool>(
            nameof(IsLoading),
            o => o.IsLoading,
            (o, v) => o.IsLoading = v);

    // Computed property IsNotLoading
    public bool IsNotLoading => !IsLoading;

    private bool _isLoading;
    private readonly ObservableCollection<object> _items;
    private string _searchText = string.Empty;
    private CancellationTokenSource? _cts;
    private readonly DispatcherTimer _debounceTimer;
    private TextBox? _textBox;

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public string? DisplayMemberPath
    {
        get => GetValue(DisplayMemberPathProperty);
        set => SetValue(DisplayMemberPathProperty, value);
    }

    public Func<object?, int>? GetItemId
    {
        get => GetValue(GetItemIdProperty);
        set => SetValue(GetItemIdProperty, value);
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

    public Func<string, int, int, Task<PagedResultDto<object>>>? LoadPageAsync
    {
        get => GetValue(LoadPageAsyncProperty);
        set => SetValue(LoadPageAsyncProperty, value);
    }

    public IReactiveCommand? AddNewCommand
    {
        get => GetValue(AddNewCommandProperty);
        set
        {
            SetValue(AddNewCommandProperty, value);
            ShowAddNew = value != null;
        }
    }

    public bool IsPopupOpen
    {
        get => GetValue(IsPopupOpenProperty);
        set => SetValue(IsPopupOpenProperty, value);
    }

    public ObservableCollection<object> Items
    {
        get => GetValue(ItemsProperty);
        private set => SetValue(ItemsProperty, value);
    }

    public int CurrentPage
    {
        get => GetValue(CurrentPageProperty);
        private set
        {
            SetValue(CurrentPageProperty, value);
            SetValue(CurrentPageInfoProperty, $"当前页: {value}");
        }
    }

    public int TotalCount
    {
        get => GetValue(TotalCountProperty);
        private set
        {
            SetValue(TotalCountProperty, value);
            SetValue(TotalCountInfoProperty, $"共{value}条记录");
        }
    }

    public string CurrentPageInfo => GetValue(CurrentPageInfoProperty);

    public string TotalCountInfo => GetValue(TotalCountInfoProperty);

    public bool ShowAddNew
    {
        get => GetValue(ShowAddNewProperty);
        private set => SetValue(ShowAddNewProperty, value);
    }

    public IReactiveCommand PageChangeCommand
    {
        get => GetValue(PageChangeCommandProperty);
        private set => SetValue(PageChangeCommandProperty, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetAndRaise(IsLoadingProperty, ref _isLoading, value);
    }

    public SearchablePageableSelectBox()
    {
        _items = new ObservableCollection<object>();
        Items = _items;
        CurrentPage = 1;
        TotalCount = 0;
        ShowAddNew = false;
        PageChangeCommand = ReactiveCommand.Create(() =>
        {
            _ = LoadPageAsyncInternal();
        });
        _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _debounceTimer.Tick += OnDebounceElapsed;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _textBox = e.NameScope.Find<TextBox>("PART_TextBox");
        if (_textBox != null)
        {
            _textBox.GotFocus += OnTextBoxGotFocus;
            _textBox.TextChanged += OnTextBoxTextChanged;
            _textBox.KeyDown += OnTextBoxKeyDown;
        }

        var itemsList = e.NameScope.Find<ListBox>("PART_ItemsList");
        if (itemsList != null)
        {
            itemsList.SelectionChanged += OnItemsListSelectionChanged;
        }

        var popup = e.NameScope.Find<Popup>("PART_Popup");
        if (popup != null)
        {
            popup.Closed += OnPopupClosed;
        }
    }

    private async void OnTextBoxGotFocus(object? sender, GotFocusEventArgs e)
    {
        // 更新 TextBox 显示文本为当前选中项的显示文本
        if (_textBox != null && SelectedItem != null)
        {
            _textBox.Text = GetDisplayText(SelectedItem);
        }
        IsPopupOpen = true;
        if (string.IsNullOrEmpty(_searchText))
        {
            _ = LoadPageAsyncInternal();
        }
    }

    private string GetDisplayText(object? item)
    {
        if (item == null) return string.Empty;
        if (!string.IsNullOrEmpty(DisplayMemberPath))
        {
            var property = item.GetType().GetProperty(DisplayMemberPath);
            if (property != null)
            {
                return property.GetValue(item)?.ToString() ?? string.Empty;
            }
        }
        return item.ToString() ?? string.Empty;
    }

    private void OnTextBoxTextChanged(object? sender, TextChangedEventArgs e)
    {
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    private async void OnDebounceElapsed(object? sender, EventArgs e)
    {
        _debounceTimer.Stop();
        if (_textBox != null)
        {
            _searchText = _textBox.Text ?? string.Empty;
        }
        CurrentPage = 1;
        await LoadPageAsyncInternal();
    }

    private void OnTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _debounceTimer.Stop();
            var textBox = sender as TextBox;
            if (textBox != null)
            {
                _searchText = textBox.Text ?? string.Empty;
            }
            CurrentPage = 1;
            _ = LoadPageAsyncInternal();
        }
        else if (e.Key == Key.Escape)
        {
            IsPopupOpen = false;
        }
    }

    private void OnItemsListSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0)
        {
            SelectedItem = e.AddedItems[0];
            IsPopupOpen = false;
        }
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        IsPopupOpen = false;
        // 重置 TextBox 显示文本为当前选中项的显示文本
        if (_textBox != null)
        {
            _textBox.Text = SelectedItem != null ? GetDisplayText(SelectedItem) : string.Empty;
        }
    }

    private async Task LoadPageAsyncInternal()
    {
        var loadFunc = LoadPageAsync;
        if (loadFunc == null)
        {
            return;
        }

        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        try
        {
            IsLoading = true;
            var result = await loadFunc(_searchText, CurrentPage, PageSize);

            _items.Clear();
            foreach (var item in result.Items ?? [])
            {
                _items.Add(item);
            }

            TotalCount = (int)result.TotalCount;
        }
        catch (TaskCanceledException)
        {
            // Ignore cancellation
        }
        catch (Exception)
        {
            _items.Clear();
            TotalCount = 0;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
