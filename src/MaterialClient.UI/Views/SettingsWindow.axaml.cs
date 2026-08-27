using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MaterialClient.Common.Events;
using MaterialClient.UI.ViewModels;
using ReactiveUI;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.UI.Views;

public partial class SettingsWindow : Window, ITransientDependency
{
    private readonly Dictionary<string, ListBoxItem> _navigationItems = new();
    private readonly CompositeDisposable _disposables = new();

    public SettingsWindow() : this(null)
    {
    }

    public SettingsWindow(IServiceProvider? serviceProvider)
    {
        InitializeComponent();
        if (Design.IsDesignMode) return;
        var viewModel = serviceProvider?.GetService(typeof(SettingsWindowViewModel)) as SettingsWindowViewModel;

        DataContext = viewModel;

        MessageBus.Current.Listen<DetailCloseRequestedMessage>()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => Close())
            .DisposeWith(_disposables);

        if (viewModel != null)
        {
            viewModel.WhenAnyValue(x => x.LprDeviceType)
                .Subscribe(_ => UpdateLprColumnVisibility(viewModel.ShowLprUserPortColumns))
                .DisposeWith(_disposables);
        }
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        Dispatcher.UIThread.Post(() =>
        {
            InitializeNavigation();

            if (DataContext is SettingsWindowViewModel viewModel)
            {
                UpdateLprColumnVisibility(viewModel.ShowLprUserPortColumns);
            }
        }, DispatcherPriority.Loaded);
    }

    private void InitializeNavigation()
    {
        _navigationItems.Clear();

        RegisterNav("ScaleSettings", "ScaleSettingsNavItem");
        RegisterNav("WeighingSettings", "WeighingSettingsNavItem");
        RegisterNav("CameraSettings", "CameraSettingsNavItem");
        RegisterNav("LprSettings", "LprSettingsNavItem");
        RegisterNav("SystemSettings", "SystemSettingsNavItem");
        RegisterNav("SoundSettings", "SoundSettingsNavItem");
        RegisterNav("PrinterSettings", "PrinterSettingsNavItem");
        RegisterNav("DocumentCameraSettings", "DocumentCameraSettingsNavItem");
        RegisterNav("AnomalySettings", "AnomalySettingsNavItem");
        RegisterNav("UrbanConfig", "UrbanConfigNavItem");

        SelectDefaultNavigation();
    }

    private void RegisterNav(string tag, string controlName)
    {
        if (this.FindControl<ListBoxItem>(controlName) is { } item)
            _navigationItems[tag] = item;
    }

    private void SelectDefaultNavigation()
    {
        if (NavigationList == null)
            return;

        if (_navigationItems.TryGetValue("ScaleSettings", out var firstNav))
        {
            NavigationList.SelectedItem = firstNav;
            ApplySelectedSection("ScaleSettings");
        }
    }

    private void OnNavigationSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (NavigationList?.SelectedItem is not ListBoxItem item)
            return;
        if (item.Tag is not string sectionTag)
            return;

        ApplySelectedSection(sectionTag);
    }

    private void ApplySelectedSection(string sectionTag)
    {
        if (DataContext is SettingsWindowViewModel viewModel)
            viewModel.SelectedSettingsSection = sectionTag;

        if (SettingsSectionsHost == null)
            return;

        foreach (var child in SettingsSectionsHost.Children)
        {
            if (child is not Control section || section.Tag is not string tag)
                continue;

            section.IsVisible = tag == sectionTag;
        }
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void UpdateLprColumnVisibility(bool isVisible)
    {
        if (LprDataGrid?.Columns == null) return;

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
        _disposables.Dispose();

        if (DataContext is SettingsWindowViewModel viewModel)
        {
            (viewModel as IDisposable)?.Dispose();
        }

        base.OnClosed(e);
    }
}
