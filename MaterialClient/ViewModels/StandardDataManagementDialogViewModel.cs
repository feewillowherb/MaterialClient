
using System.Collections.ObjectModel;

using System.Windows.Input;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Models;
using MaterialClient.Common.Services;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.ViewModels;

public record DeliveryTypeFilterOption(string DisplayName, DeliveryType? Value);

public record OrderTypeFilterOption(string DisplayName, OrderTypeEnum? Value);

public partial class StandardDataManagementDialogViewModel : ViewModelBase, ITransientDependency
{
    private readonly IStandardModeService _standardModeService;

    public static IReadOnlyList<DeliveryTypeFilterOption> DeliveryTypeFilterOptions { get; } =
    [
        new DeliveryTypeFilterOption("全部", null),
        new DeliveryTypeFilterOption("收料", DeliveryType.Receiving),
        new DeliveryTypeFilterOption("发料", DeliveryType.Sending)
    ];

    public static IReadOnlyList<OrderTypeFilterOption> OrderTypeFilterOptions { get; } =
    [
        new OrderTypeFilterOption("全部", null),
        new OrderTypeFilterOption("首称中", OrderTypeEnum.FirstWeight),
        new OrderTypeFilterOption("已完成", OrderTypeEnum.Completed),
        new OrderTypeFilterOption("已取消", OrderTypeEnum.Esc)
    ];

    public StandardDataManagementDialogViewModel(
        IStandardModeService standardModeService,
        ILogger<StandardDataManagementDialogViewModel>? logger = null)
        : base(logger)
    {
        _standardModeService = standardModeService;
        Records = new ObservableCollection<StandardExportRow>();
        CurrentPage = 1;
        TotalPages = 1;
        SelectedDeliveryType = DeliveryTypeFilterOptions[0];
        SelectedOrderType = OrderTypeFilterOptions[0];

        LoadDataCommand = ReactiveCommand.CreateFromTask(LoadDataAsync);
    }

    public ObservableCollection<StandardExportRow> Records { get; }

    [Reactive] public DateTime? StartDate { get; set; }
    [Reactive] public DateTime? EndDate { get; set; }
    [Reactive] public string PlateNumber { get; set; } = string.Empty;
    [Reactive] public string MaterialName { get; set; } = string.Empty;
    [Reactive] public DeliveryTypeFilterOption? SelectedDeliveryType { get; set; }
    [Reactive] public OrderTypeFilterOption? SelectedOrderType { get; set; }

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
            var result = await _standardModeService.GetPagedExportRowsAsync(
                filter, CurrentPage, PageSize);

            Records.Clear();
            foreach (var row in result.Items)
                Records.Add(row);

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
            Logger?.LogError(ex, "加载标准台账分页数据失败，回退到测试数据。");

            Records.Clear();
            Records.Add(CreateTestRow());
            TotalCount = 1;
            TotalPages = 1;
            CurrentPage = 1;
        }
    }

    private StandardExportFilter BuildFilter()
    {
        return new StandardExportFilter
        {
            WeighingMode = WeighingMode.Standard,
            StartDate = StartDate,
            EndDate = EndDate,
            PlateNumber = string.IsNullOrWhiteSpace(PlateNumber) ? null : PlateNumber,
            MaterialName = string.IsNullOrWhiteSpace(MaterialName) ? null : MaterialName,
            DeliveryType = SelectedDeliveryType?.Value,
            OrderType = SelectedOrderType?.Value
        };
    }

    private static StandardExportRow CreateTestRow()
    {
        return new StandardExportRow
        {
            PlateNumber = "浙A12345",
            DeliveryType = "收料",
            MaterialName = "水泥",
            OrderType = "已完成",
            PlanQuantity = 100,
            PlanWeight = 5000m,
            OffsetCount = 0.5m,
            ActualQuantity = 99,
            ActualWeight = 4950m,
            UnitConversion = 50,
            JoinTime = "2025-11-18 09:32:01",
            OutTime = "2025-11-18 10:01:23",
            ProviderName = "测试供应商",
            OrderNo = "sl-20251118093201-0001",
            Remark = "标准模式测试数据"
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
