using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using MaterialClient.ViewModels;

namespace MaterialClient.Views;

public partial class SearchableSelectionBox : UserControl
{
    public static readonly StyledProperty<bool> IsPopupOpenProperty =
        AvaloniaProperty.Register<SearchableSelectionBox, bool>(nameof(IsPopupOpen), defaultValue: false);

    public static readonly StyledProperty<string?> PlaceholderTextProperty =
        AvaloniaProperty.Register<SearchableSelectionBox, string?>(nameof(PlaceholderText), defaultValue: "请选择");

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
        UpdateDisplayText();
    }

    private void Npc_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IGenericSelectionPopupBindings.SelectedDisplayText))
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
            }
            else
            {
                DisplayTextBlock.Text = PlaceholderText ?? "请选择";
                DisplayTextBlock.Foreground = new SolidColorBrush(Color.Parse("#999999"));
            }
        }
    }

    private void OnIsPopupOpenChanged(bool isOpen)
    {
        if (DisplayPanel == null || SearchTextBox == null) return;

        DisplayPanel.IsVisible = !isOpen;
        SearchTextBox.IsVisible = isOpen;

        if (isOpen)
        {
            // Defer focus to next UI tick to avoid re-entrancy / layout deadlock when opening
            var box = SearchTextBox;
            Dispatcher.UIThread.Post(() => box.Focus(), DispatcherPriority.Loaded);
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
