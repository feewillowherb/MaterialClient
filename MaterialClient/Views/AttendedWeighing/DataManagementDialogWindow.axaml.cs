using System;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MaterialClient.Common.Models;
using MaterialClient.Common.Services;
using MaterialClient.ViewModels;

namespace MaterialClient.Views.AttendedWeighing;

public partial class DataManagementDialogWindow : Window
{
    private readonly WindowNotificationManager? _notificationManager;
    private readonly DataManagementDialogViewModel _vm;

    public DataManagementDialogWindow(
        ISolidWasteService solidWasteService,
        IExcelExportService exportService,
        WindowNotificationManager? notificationManager = null)
    {
        _notificationManager = notificationManager;
        _vm = new DataManagementDialogViewModel(solidWasteService, exportService);
        InitializeComponent();
        DataContext = _vm;
        // 初次打开窗口时触发一次数据加载（命令内部处理异步）
        _vm.LoadDataCommand.Execute(null);
    }

    private void OnQueryClick(object? sender, RoutedEventArgs e)
    {
        _vm.CurrentPage = 1;
        _vm.LoadDataCommand.Execute(null);
    }

    private async void OnExportClick(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { Title = "选择导出目录" });
        if (folders.Count == 0) return;

        var savePath = folders[0].Path.LocalPath;
        var fileName = $"固废运单_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
        var outputPath = System.IO.Path.Combine(savePath, fileName);

        var result = await _vm.ExportCommand.Execute(outputPath);
        if (_notificationManager != null)
        {
            if (result.Success)
                _notificationManager.Show(new Notification("导出成功",
                    $"已导出 {result.RowCount} 条到 {fileName}",
                    NotificationType.Success));
            else
                _notificationManager.Show(new Notification("导出失败",
                    "导出过程中发生错误，请重试",
                    NotificationType.Error));
        }
    }

    private void OnCloseButtonClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void OnConfirmButtonClick(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }
}
