using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MaterialClient.UI.ViewModels;
using ReactiveUI;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.UI.Views;

public partial class SettingsWindow : Window, ITransientDependency
{
    private readonly Dictionary<string, Border> _sectionElements = new();
    private readonly Dictionary<string, ListBoxItem> _navigationItems = new();
    private bool _isNavigationClick = false; // Prevent recursive updates


    public SettingsWindow() : this(null)
    {
    }

    public SettingsWindow(IServiceProvider? serviceProvider)
    {
        InitializeComponent();
        if (Design.IsDesignMode) return;
        var viewModel = serviceProvider?.GetService(typeof(SettingsWindowViewModel)) as SettingsWindowViewModel;


        DataContext = viewModel;

        // Subscribe to close requested event
        viewModel?.CloseRequested += OnCloseRequested;

        // Subscribe to LprDeviceType changes to update column visibility
        if (viewModel != null)
        {
            viewModel.WhenAnyValue(x => x.LprDeviceType)
                .Subscribe(_ => UpdateLprColumnVisibility(viewModel.ShowHikvisionLprFields));
        }
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        // Initialize section tracking after window is loaded
        Dispatcher.UIThread.Post(() => 
        { 
            InitializeSectionTracking();
            
            // Initialize LPR column visibility based on current device type
            if (DataContext is SettingsWindowViewModel viewModel)
            {
                UpdateLprColumnVisibility(viewModel.ShowHikvisionLprFields);
            }
        }, DispatcherPriority.Loaded);
    }

    private void InitializeSectionTracking()
    {
        // Map navigation items to sections
        if (this.FindControl<ListBoxItem>("ScaleSettingsNavItem") is { } scaleNav)
            _navigationItems["ScaleSettings"] = scaleNav;
        if (this.FindControl<ListBoxItem>("WeighingSettingsNavItem") is { } weighingNav)
            _navigationItems["WeighingSettings"] = weighingNav;
        if (this.FindControl<ListBoxItem>("CameraSettingsNavItem") is { } cameraNav)
            _navigationItems["CameraSettings"] = cameraNav;
        if (this.FindControl<ListBoxItem>("LprSettingsNavItem") is { } lprNav)
            _navigationItems["LprSettings"] = lprNav;
        if (this.FindControl<ListBoxItem>("SystemSettingsNavItem") is { } systemNav)
            _navigationItems["SystemSettings"] = systemNav;
        if (this.FindControl<ListBoxItem>("SoundSettingsNavItem") is { } soundNav)
            _navigationItems["SoundSettings"] = soundNav;
        if (this.FindControl<ListBoxItem>("PrinterSettingsNavItem") is { } printerNav)
            _navigationItems["PrinterSettings"] = printerNav;

        // Map section borders
        if (this.FindControl<Border>("ScaleSettings") is { } scaleBorder)
            _sectionElements["ScaleSettings"] = scaleBorder;
        if (this.FindControl<Border>("WeighingSettings") is { } weighingBorder)
            _sectionElements["WeighingSettings"] = weighingBorder;
        if (this.FindControl<Border>("CameraSettings") is { } cameraBorder)
            _sectionElements["CameraSettings"] = cameraBorder;
        if (this.FindControl<Border>("LprSettings") is { } lprBorder)
            _sectionElements["LprSettings"] = lprBorder;
        if (this.FindControl<Border>("SystemSettings") is { } systemBorder)
            _sectionElements["SystemSettings"] = systemBorder;
        if (this.FindControl<Border>("SoundSettings") is { } soundBorder)
            _sectionElements["SoundSettings"] = soundBorder;
        if (this.FindControl<Border>("PrinterSettings") is { } printerBorder)
            _sectionElements["PrinterSettings"] = printerBorder;

        // Subscribe to scroll events
        if (this.FindControl<ScrollViewer>("ContentScrollViewer") is { } scrollViewer)
        {
            scrollViewer.GetObservable(ScrollViewer.OffsetProperty)
                .Throttle(TimeSpan.FromMilliseconds(100))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => OnContentScrollChanged());
        }

        // Set initial selection
        if (NavigationList != null && _navigationItems.TryGetValue("ScaleSettings", out var firstNav))
        {
            NavigationList.SelectedItem = firstNav;
        }
    }

    private void OnContentScrollChanged()
    {
        if (_isNavigationClick) return; // Ignore during programmatic scroll

        if (this.FindControl<ScrollViewer>("ContentScrollViewer") is not { } scrollViewer) return;
        if (scrollViewer.Content is not StackPanel contentPanel) return;

        var scrollOffset = scrollViewer.Offset.Y;
        var viewportHeight = scrollViewer.Viewport.Height;
        var threshold = 50.0; // Threshold for considering a section as "active" (50px from top)

        // Find the section that is currently at or near the top of the viewport
        string? activeSection = null;
        double bestScore = double.MaxValue;

        foreach (var (tag, border) in _sectionElements)
        {
            // Get section position relative to the content panel
            var sectionPoint = border.TranslatePoint(new Point(0, 0), contentPanel);
            if (!sectionPoint.HasValue) continue;

            var sectionTop = sectionPoint.Value.Y;
            var sectionBottom = sectionTop + border.Bounds.Height;

            // Check if section is visible in viewport
            var sectionTopInViewport = sectionTop - scrollOffset;
            var sectionBottomInViewport = sectionBottom - scrollOffset;

            if (sectionBottomInViewport > 0 && sectionTopInViewport < viewportHeight)
            {
                // Calculate a score: lower is better
                // Prefer sections whose top is at or slightly above the viewport top
                double score;

                if (sectionTopInViewport <= threshold)
                {
                    // Section top is at or above threshold - this is ideal
                    // Score is the distance from threshold (closer to threshold = better)
                    score = Math.Abs(sectionTopInViewport - threshold);
                }
                else
                {
                    // Section top is below threshold - less ideal
                    // Add a penalty to make it less preferred
                    score = sectionTopInViewport - threshold + 1000;
                }

                if (score < bestScore)
                {
                    bestScore = score;
                    activeSection = tag;
                }
            }
        }

        // Update navigation selection
        if (activeSection != null &&
            _navigationItems.TryGetValue(activeSection, out var navItem) &&
            NavigationList != null &&
            NavigationList.SelectedItem != navItem) // Only update if different to avoid unnecessary updates
        {
            NavigationList.SelectedItem = navItem;
        }
    }

    private async void OnNavigationSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (NavigationList?.SelectedItem is not ListBoxItem item) return;
        if (item.Tag is not string sectionTag) return;

        if (!_sectionElements.TryGetValue(sectionTag, out var targetBorder)) return;

        _isNavigationClick = true;

        // Get target position relative to ScrollViewer's content
        if (this.FindControl<ScrollViewer>("ContentScrollViewer") is { } scrollViewer)
        {
            // Get the parent StackPanel that contains all sections
            if (scrollViewer.Content is StackPanel contentPanel)
            {
                // Calculate the position of the target border relative to the StackPanel
                var targetPoint = targetBorder.TranslatePoint(new Point(0, 0), contentPanel);
                if (targetPoint.HasValue)
                {
                    // Scroll to section - Offset.Y is the scroll position in the content
                    // Add a small offset to position the section header near the top
                    var scrollOffset = Math.Max(0, targetPoint.Value.Y - 10);
                    scrollViewer.Offset = new Vector(scrollViewer.Offset.X, scrollOffset);
                }
            }
            else
            {
                // Fallback: try TranslatePoint to scrollViewer directly
                var targetPoint = targetBorder.TranslatePoint(new Point(0, 0), scrollViewer);
                if (targetPoint.HasValue)
                {
                    // If the point is relative to viewport, we need to add current offset
                    var scrollOffset = scrollViewer.Offset.Y + targetPoint.Value.Y - 10;
                    scrollViewer.Offset = new Vector(scrollViewer.Offset.X, Math.Max(0, scrollOffset));
                }
            }
        }

        // Reset flag after a delay to allow scroll animation to complete
        await Task.Delay(300);
        _isNavigationClick = false;
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        Close();
    }

    private void UpdateLprColumnVisibility(bool isVisible)
    {
        if (LprDataGrid?.Columns == null) return;

        // Update Hikvision-specific columns visibility by header name
        var hikvisionHeaders = new[] { "用户名", "端口" };
        
        foreach (var column in LprDataGrid.Columns)
        {
            if (column is DataGridTextColumn textColumn && 
                textColumn.Header?.ToString() is { } header &&
                hikvisionHeaders.Contains(header))
            {
                textColumn.IsVisible = isVisible;
            }
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        // Unsubscribe from event and dispose ViewModel (MessageBus subscription)
        if (DataContext is SettingsWindowViewModel viewModel)
        {
            viewModel.CloseRequested -= OnCloseRequested;
            (viewModel as IDisposable)?.Dispose();
        }
        base.OnClosed(e);
    }
}