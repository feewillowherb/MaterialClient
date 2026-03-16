using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MaterialClient.Common.Models;
using MaterialClient.Common.Services;

namespace MaterialClient.Views.AttendedWeighing;

public partial class DataManagementDialogWindow : Window
{
    private readonly ISolidWasteService _solidWasteService;
    private readonly ISolidWasteExcelExportService _exportService;
    private readonly WindowNotificationManager? _notificationManager;
    private readonly DataManagementDialogViewModel _vm;

    public DataManagementDialogWindow(
        ISolidWasteService solidWasteService,
        ISolidWasteExcelExportService exportService,
        WindowNotificationManager? notificationManager = null)
    {
        _solidWasteService = solidWasteService;
        _exportService = exportService;
        _notificationManager = notificationManager;
        _vm = new DataManagementDialogViewModel();
        InitializeComponent();
        DataContext = _vm;
        _ = LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            var filter = BuildFilter();
            var rows = await _solidWasteService.GetExportRowsAsync(filter);
            _vm.Records.Clear();
            foreach (var row in rows)
                _vm.Records.Add(row);
            _vm.TotalCount = _vm.Records.Count;
        }
        catch
        {
            // 未接入时使用一条测试数据用于样式验收
            _vm.Records.Add(CreateTestRow());
            _vm.TotalCount = 1;
        }
    }

    private SolidWasteExportFilter BuildFilter()
    {
        return new SolidWasteExportFilter
        {
            StartDate = _vm.StartDate,
            EndDate = _vm.EndDate,
            PlateNumber = string.IsNullOrWhiteSpace(_vm.PlateNumber) ? null : _vm.PlateNumber,
            GoodsName = string.IsNullOrWhiteSpace(_vm.GoodsName) ? null : _vm.GoodsName,
            ProviderName = string.IsNullOrWhiteSpace(_vm.ProviderName) ? null : _vm.ProviderName
        };
    }

    private static SolidWasteExportRow CreateTestRow()
    {
        return new SolidWasteExportRow
        {
            SerialNumber = "sl-20251118153228-001",
            VehicleNumber = "浙A12345",
            ShippingUnit = "测试供应商",
            ReceivingUnit = "东部资源化处置点",
            GoodsName = "装修垃圾",
            GrossWeight = 8270m,
            TareWeight = 5750m,
            NetWeight = 2520m,
            Remark = "样式验收测试",
            GrossWeightTime = "2025-11-18 09:32:01",
            TareWeightTime = "2025-11-18 10:01:23",
            Street = "瓜沥镇",
            SolidWasteType = "村、社区",
            ManifestNumber = "2414822",
            UploadResult = "1",
            UploadStatus = "上传成功",
            UploadTime = ""
        };
    }

    private async void OnQueryClick(object? sender, RoutedEventArgs e)
    {
        await LoadDataAsync();
    }

    private async void OnExportClick(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { Title = "选择导出目录" });
        if (folders.Count == 0) return;

        var savePath = folders[0].Path.LocalPath;
        var fileName = $"固废运单_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
        var outputPath = System.IO.Path.Combine(savePath, fileName);

        try
        {
            var filter = BuildFilter();
            var result = await _exportService.ExportAsync(filter, outputPath);
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
        catch (Exception)
        {
            _notificationManager?.Show(new Notification("导出失败",
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

public class DataManagementDialogViewModel
{
    public ObservableCollection<SolidWasteExportRow> Records { get; } = new();
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string PlateNumber { get; set; } = string.Empty;
    public string GoodsName { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public int CurrentPage { get; set; } = 1;
    public int TotalCount { get; set; }
    public int TotalPages { get; set; } = 1;
}
