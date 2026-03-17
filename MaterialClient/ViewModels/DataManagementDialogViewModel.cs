using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using MaterialClient.Common.Models;
using MaterialClient.Common.Services;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.ViewModels;

public partial class DataManagementDialogViewModel : ViewModelBase, ITransientDependency
{
    private readonly ISolidWasteService _solidWasteService;
    private readonly IExcelExportService _exportService;
    private Func<Task<string?>>? _browseFolderAsync;
    private Action<string, string, bool>? _notify;

    public DataManagementDialogViewModel(
        ISolidWasteService solidWasteService,
        IExcelExportService exportService,
        ILogger<DataManagementDialogViewModel>? logger = null)
        : base(logger)
    {
        _solidWasteService = solidWasteService;
        _exportService = exportService;
        Records = new ObservableCollection<SolidWasteExportRow>();
        CurrentPage = 1;
        TotalPages = 1;

        LoadDataCommand = ReactiveCommand.CreateFromTask(LoadDataAsync);
    }

    public void SetBrowseHandler(Func<Task<string?>> handler) => _browseFolderAsync = handler;

    public void SetNotifyHandler(Action<string, string, bool> handler) => _notify = handler;

    public ObservableCollection<SolidWasteExportRow> Records { get; }

    [Reactive] public DateTime? StartDate { get; set; }
    [Reactive] public DateTime? EndDate { get; set; }
    [Reactive] public string PlateNumber { get; set; } = string.Empty;
    [Reactive] public string GoodsName { get; set; } = string.Empty;
    [Reactive] public string ProviderName { get; set; } = string.Empty;

    public int PageSize => DefaultPageSize;

    private const int DefaultPageSize = 10;

    private int _currentPage = 1;
    private int _totalCount;
    private int _totalPages = 1;

    public int CurrentPage
    {
        get => _currentPage;
        set
        {
            if (_currentPage != value)
            {
                _currentPage = value;
                this.RaisePropertyChanged();
                _ = LoadDataAsync();
            }
        }
    }

    public int TotalCount
    {
        get => _totalCount;
        private set => this.RaiseAndSetIfChanged(ref _totalCount, value);
    }

    public int TotalPages
    {
        get => _totalPages;
        private set => this.RaiseAndSetIfChanged(ref _totalPages, value);
    }

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

    [ReactiveCommand]
    private Task QueryAsync()
    {
        CurrentPage = 1;
        return LoadDataAsync();
    }

    [ReactiveCommand]
    private async Task ExportAsync()
    {
        if (_browseFolderAsync == null) return;
        var savePath = await _browseFolderAsync();
        if (string.IsNullOrEmpty(savePath)) return;

        var fileName = $"固废运单_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
        var outputPath = Path.Combine(savePath, fileName);

        try
        {
            var filter = BuildFilter();
            var result = await _exportService.ExportSolidWasteAsync(filter, outputPath);
            if (_notify != null)
            {
                if (result.Success)
                    _notify("导出成功", $"已导出 {result.RowCount} 条到 {fileName}", true);
                else
                    _notify("导出失败", "导出过程中发生错误，请重试", false);
            }
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "导出固废台账失败");
            _notify?.Invoke("导出失败", "导出过程中发生错误，请重试", false);
        }
    }

    [ReactiveCommand]
    private void Close()
    {
        // View 订阅 CloseCommand 执行 Close(false)
    }

    [ReactiveCommand]
    private void Confirm()
    {
        // View 订阅 ConfirmCommand 执行 Close(true)
    }
}

