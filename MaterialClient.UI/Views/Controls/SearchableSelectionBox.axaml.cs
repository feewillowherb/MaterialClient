using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using MaterialClient.ViewModels;

namespace MaterialClient.Views.Controls;

public partial class SearchableSelectionBox : UserControl
{
    public static readonly StyledProperty<bool> IsPopupOpenProperty =
        AvaloniaProperty.Register<SearchableSelectionBox, bool>(nameof(IsPopupOpen), defaultValue: false);

    public static readonly StyledProperty<string?> PlaceholderTextProperty =
        AvaloniaProperty.Register<SearchableSelectionBox, string?>(nameof(PlaceholderText), defaultValue: "请选择");

    /// <summary>
    /// 弹窗打开期间缓存的已选显示文本；弹窗关闭后若 VM 的已选项被清空，则用此值恢复显示。
    /// </summary>
    private string? _cachedDisplayTextWhenPopupOpen;

    /// <summary>
    /// 为 true 表示当前显示的是“关闭弹窗时从缓存恢复”的文本（VM 已选项为空但界面显示缓存值）。
    /// </summary>
    private bool _displayRestoredFromCache;

    public bool IsPopupOpen
    {
        get => GetValue(IsPopupOpenProperty);
        set => SetValue(IsPopupOpenProperty, value);
    }

    public string? PlaceholderText
    {
        get => GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    public SearchableSelectionBox()
    {
        InitializeComponent();
        Focusable = true;

        this.GetObservable(IsPopupOpenProperty).Subscribe(OnIsPopupOpenChanged);
        this.GetObservable(DataContextProperty).Subscribe(_ => OnDataContextChanged());
    }

    private void OnDataContextChanged()
    {
        if (DataContext is INotifyPropertyChanged npc)
        {
            npc.PropertyChanged -= Npc_PropertyChanged;
            npc.PropertyChanged += Npc_PropertyChanged;
        }

        _cachedDisplayTextWhenPopupOpen = null;
        _displayRestoredFromCache = false;

        UpdateDisplayText();
    }

    private void Npc_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(IGenericSelectionPopupBindings.SelectedDisplayText))
            return;

        if (DataContext is IGenericSelectionPopupBindings vm)
        {
            var text = vm.SelectedDisplayText;
            if (IsPopupOpen && !string.IsNullOrEmpty(text))
            {
                _cachedDisplayTextWhenPopupOpen = text;
            }

            if (!string.IsNullOrEmpty(text))
            {
                _displayRestoredFromCache = false;
            }
        }

        UpdateDisplayText();
    }

    private void UpdateDisplayText()
    {
        if (DisplayTextBlock == null) return;

        if (DataContext is IGenericSelectionPopupBindings vm)
        {
            var text = vm.SelectedDisplayText;
            if (!string.IsNullOrEmpty(text))
            {
                DisplayTextBlock.Text = text;
                DisplayTextBlock.Foreground = new SolidColorBrush(Color.Parse("#333333"));
                return;
            }

            // VM 已选项为空：若处于“从缓存恢复”状态则继续显示缓存文本
            if (_displayRestoredFromCache && !string.IsNullOrEmpty(_cachedDisplayTextWhenPopupOpen))
            {
                DisplayTextBlock.Text = _cachedDisplayTextWhenPopupOpen;
                DisplayTextBlock.Foreground = new SolidColorBrush(Color.Parse("#333333"));
                return;
            }

            DisplayTextBlock.Text = PlaceholderText ?? "请选择";
            DisplayTextBlock.Foreground = new SolidColorBrush(Color.Parse("#999999"));
        }
    }

    private void OnIsPopupOpenChanged(bool isOpen)
    {
        if (DisplayPanel == null || SearchTextBox == null) return;

        DisplayPanel.IsVisible = !isOpen;
        SearchTextBox.IsVisible = isOpen;

        if (isOpen)
        {
            _displayRestoredFromCache = false;

            if (DataContext is IGenericSelectionPopupBindings vm &&
                !string.IsNullOrEmpty(vm.SelectedDisplayText))
            {
                _cachedDisplayTextWhenPopupOpen = vm.SelectedDisplayText;
            }

            // Defer focus to next UI tick to avoid re-entrancy / layout deadlock when opening
            var box = SearchTextBox;
            Dispatcher.UIThread.Post(() => box.Focus(), DispatcherPriority.Loaded);
        }
        else
        {
            // 弹窗关闭后：若已选项被清空但存在缓存，则从缓存恢复显示
            if (DataContext is IGenericSelectionPopupBindings vm &&
                string.IsNullOrEmpty(vm.SelectedDisplayText) &&
                !string.IsNullOrEmpty(_cachedDisplayTextWhenPopupOpen))
            {
                _displayRestoredFromCache = true;

                if (DisplayTextBlock != null)
                {
                    DisplayTextBlock.Text = _cachedDisplayTextWhenPopupOpen;
                    DisplayTextBlock.Foreground = new SolidColorBrush(Color.Parse("#333333"));
                }
            }
        }
    }

    private void OnRootPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsPopupOpen)
            IsPopupOpen = true;
    }

    private void OnDisplayPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsPopupOpen)
            IsPopupOpen = true;
    }

    protected override void OnGotFocus(GotFocusEventArgs e)
    {
        base.OnGotFocus(e);
        if (!IsPopupOpen)
            IsPopupOpen = true;
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateDisplayText();
    }
}
