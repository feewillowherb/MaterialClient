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

/// <summary>
/// Unified searchable selection ComboBox component.
/// Acts as both selection display and search input, with automatic popup management.
/// </summary>
public partial class SearchableComboBox : UserControl
{
    public static readonly StyledProperty<bool> IsPopupOpenProperty =
        AvaloniaProperty.Register<SearchableComboBox, bool>(nameof(IsPopupOpen), defaultValue: false);

    public static readonly StyledProperty<string?> PlaceholderTextProperty =
        AvaloniaProperty.Register<SearchableComboBox, string?>(nameof(PlaceholderText), defaultValue: "请选择");

    public static readonly StyledProperty<string?> SelectedDisplayTextProperty =
        AvaloniaProperty.Register<SearchableComboBox, string?>(nameof(SelectedDisplayText), defaultValue: null);

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

    public string? SelectedDisplayText
    {
        get => GetValue(SelectedDisplayTextProperty);
        set => SetValue(SelectedDisplayTextProperty, value);
    }

    public SearchableComboBox()
    {
        InitializeComponent();
        Focusable = true;

        this.GetObservable(IsPopupOpenProperty).Subscribe(OnIsPopupOpenChanged);
        this.GetObservable(SelectedDisplayTextProperty).Subscribe(OnSelectedDisplayTextChanged);
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
        // Support both old and new ViewModel interfaces
        if (e.PropertyName == nameof(IGenericSelectionPopupBindings.SelectedDisplayText) ||
            e.PropertyName == nameof(ISearchableSelection<object>.SelectedDisplayText))
        {
            UpdateDisplayText();
        }
    }

    private void OnSelectedDisplayTextChanged(string? displayText)
    {
        if (DisplayTextBlock == null) return;

        if (!string.IsNullOrEmpty(displayText))
        {
            DisplayTextBlock.Text = displayText;
            DisplayTextBlock.Foreground = new SolidColorBrush(Color.Parse("#333333"));
        }
        else
        {
            DisplayTextBlock.Text = PlaceholderText ?? "请选择";
            DisplayTextBlock.Foreground = new SolidColorBrush(Color.Parse("#999999"));
        }
    }

    private void UpdateDisplayText()
    {
        if (DisplayTextBlock == null) return;

        string? displayText = null;

        // Try new interface first
        if (DataContext is ISearchableSelection<object> newVm)
        {
            displayText = newVm.SelectedDisplayText;
        }
        // Fallback to old interface
        else if (DataContext is IGenericSelectionPopupBindings oldVm)
        {
            displayText = oldVm.SelectedDisplayText;
        }

        SelectedDisplayText = displayText;

        if (!string.IsNullOrEmpty(displayText))
        {
            DisplayTextBlock.Text = displayText;
            DisplayTextBlock.Foreground = new SolidColorBrush(Color.Parse("#333333"));
        }
        else
        {
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
            // Defer focus to next UI tick to avoid re-entrancy / layout deadlock when opening
            var box = SearchTextBox;
            Dispatcher.UIThread.Post(() => box.Focus(), DispatcherPriority.Loaded);

            // Bind SearchText to ViewModel
            if (DataContext is ISearchableSelection<object> newVm)
            {
                SearchTextBox.Bind(TextBox.TextProperty, new Binding("SearchText", BindingMode.TwoWay));
            }
            else if (DataContext is IGenericSelectionPopupBindings oldVm)
            {
                SearchTextBox.Bind(TextBox.TextProperty, new Binding("SearchText", BindingMode.TwoWay));
            }
        }
        else
        {
            // Unbind when closed to avoid memory leaks
            SearchTextBox.ClearValue(TextBox.TextProperty);
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

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        // Handle keyboard navigation
        if (e.Key == Key.Escape && IsPopupOpen)
        {
            IsPopupOpen = false;
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && IsPopupOpen)
        {
            // Let the popup handle Enter key
            e.Handled = false;
        }
    }
}
