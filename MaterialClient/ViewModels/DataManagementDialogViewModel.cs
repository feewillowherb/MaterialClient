using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using MaterialClient.Common.Models;
using MaterialClient.Common.Services;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace MaterialClient.ViewModels;

public partial class DataManagementDialogViewModel : ViewModelBase
{
    private readonly ISolidWasteService _solidWasteService;

    public DataManagementDialogViewModel(
        ISolidWasteService solidWasteService,
        ILogger<DataManagementDialogViewModel>? logger = null)
        : base(logger)
    {
        _solidWasteService = solidWasteService;
        Records = new ObservableCollection<SolidWasteExportRow>();
        CurrentPage = 1;
        TotalPages = 1;

        LoadDataCommand = ReactiveCommand.CreateFromTask(LoadDataAsync);
    }

    public ObservableCollection<SolidWasteExportRow> Records { get; }

    [Reactive] public DateTime? StartDate { get; set; }
    [Reactive] public DateTime? EndDate { get; set; }
    [Reactive] public string PlateNumber { get; set; } = string.Empty;
    [Reactive] public string GoodsName { get; set; } = string.Empty;
    [Reactive] public string ProviderName { get; set; } = string.Empty;

    public int PageSize => DefaultPageSize;

    private const int DefaultPageSize = 10;

    [Reactive] public int CurrentPage { get; set; }

    [Reactive] public int TotalCount { get; set; }

    [Reactive] public int TotalPages { get; set; }

    public ICommand LoadDataCommand { get; }

    private async Task LoadDataAsync()
    {
        try
        {
            var filter = BuildFilter();
            var result = await _solidWasteService.GetPagedExportRowsAsync(
                filter, CurrentPage, PageSize);

            Records.Clear();
            foreach (var row in result.Items)
                Records.Add(row);

            // TotalCount 在 DTO 中为 long，这里显式转换为 int 仅用于分页显示。
            // 业务上假设固废台账分页总数不会超过 int.MaxValue。
            TotalCount = (int)result.TotalCount;
            TotalPages = result.TotalCount > 0
                ? (int)Math.Ceiling(result.TotalCount / (double)PageSize)
                : 1;

            if (CurrentPage > TotalPages && TotalPages > 0)
                CurrentPage = TotalPages;
            if (CurrentPage < 1)
                CurrentPage = 1;
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "加载固废台账分页数据失败，回退到测试数据。");

            Records.Clear();
            Records.Add(CreateTestRow());
            TotalCount = 1;
            TotalPages = 1;
            CurrentPage = 1;
        }
    }

    private SolidWasteExportFilter BuildFilter()
    {
        return new SolidWasteExportFilter
        {
            StartDate = StartDate,
            EndDate = EndDate,
            PlateNumber = string.IsNullOrWhiteSpace(PlateNumber) ? null : PlateNumber,
            GoodsName = string.IsNullOrWhiteSpace(GoodsName) ? null : GoodsName,
            ProviderName = string.IsNullOrWhiteSpace(ProviderName) ? null : ProviderName
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

    /// <summary>
    ///     分页变化命令（Ursa.Pagination 用），由 XAML 直接绑定生成的 PageChangeCommand 调用。
    ///     Ursa 通过 TwoWay 绑定更新 CurrentPage，然后执行该无参数命令。
    /// </summary>
    [ReactiveCommand]
    private Task PageChangeAsync() => LoadDataAsync();
}

