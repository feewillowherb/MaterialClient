using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace MaterialClient.ViewModels;

public record DeliveryTypeFilterOption(string DisplayName, DeliveryType? Value);

public record OrderTypeFilterOption(string DisplayName, OrderTypeEnum? Value);

public partial class StandardDataManagementDialogViewModel : ViewModelBase, ITransientDependency
{
    private readonly IRepository<Waybill, long> _waybillRepository;
    private readonly IRepository<Material, int> _materialRepository;
    private readonly IRepository<Provider, int> _providerRepository;

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
        IRepository<Waybill, long> waybillRepository,
        IRepository<Material, int> materialRepository,
        IRepository<Provider, int> providerRepository,
        ILogger<StandardDataManagementDialogViewModel>? logger = null)
        : base(logger)
    {
        _waybillRepository = waybillRepository;
        _materialRepository = materialRepository;
        _providerRepository = providerRepository;
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
            var queryable = await _waybillRepository.GetQueryableAsync();

            // 基础过滤：标准模式且未删除
            queryable = queryable.Where(w =>
                w.WeighingMode == WeighingMode.Standard && !w.IsDeleted);

            // 应用筛选条件（MaterialName 需要异步查询匹配的 ID）
            queryable = await ApplyFiltersAsync(queryable);

            var totalCount = await queryable.CountAsync();
            TotalCount = totalCount;
            TotalPages = totalCount > 0
                ? (int)Math.Ceiling(totalCount / (double)PageSize)
                : 1;

            if (CurrentPage > TotalPages && TotalPages > 0)
                CurrentPage = TotalPages;
            if (CurrentPage < 1)
                CurrentPage = 1;

            // 按 JoinTime 降序排列，分页
            var pagedWaybills = await queryable
                .OrderByDescending(w => w.JoinTime)
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            // 构建关联字典
            var providerDict = await BuildProviderDictAsync(pagedWaybills);
            var materialDict = await BuildMaterialDictAsync(pagedWaybills);

            Records.Clear();
            foreach (var waybill in pagedWaybills)
                Records.Add(MapToExportRow(waybill, providerDict, materialDict));
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

    private async Task<IQueryable<Waybill>> ApplyFiltersAsync(IQueryable<Waybill> queryable)
    {
        if (!string.IsNullOrWhiteSpace(PlateNumber))
            queryable = queryable.Where(w =>
                w.PlateNumber != null && w.PlateNumber.Contains(PlateNumber));

        if (SelectedDeliveryType is { Value: not null })
            queryable = queryable.Where(w => w.DeliveryType == SelectedDeliveryType.Value);

        if (SelectedOrderType is { Value: not null })
            queryable = queryable.Where(w => w.OrderType == SelectedOrderType.Value);

        if (StartDate.HasValue)
            queryable = queryable.Where(w =>
                w.JoinTime != null && w.JoinTime >= StartDate.Value);

        if (EndDate.HasValue)
            queryable = queryable.Where(w =>
                w.JoinTime != null && w.JoinTime <= EndDate.Value.AddDays(1));

        if (!string.IsNullOrWhiteSpace(MaterialName))
        {
            var matchedMaterialIds = (await _materialRepository.GetQueryableAsync())
                .Where(m => m.Name.Contains(MaterialName))
                .Select(m => m.Id)
                .ToHashSet();

            queryable = queryable.Where(w =>
                w.MaterialId.HasValue && matchedMaterialIds.Contains(w.MaterialId.Value));
        }

        return queryable;
    }

    private async Task<Dictionary<int, string>> BuildProviderDictAsync(List<Waybill> waybills)
    {
        var providerIds = waybills
            .Where(w => w.ProviderId.HasValue)
            .Select(w => w.ProviderId!.Value)
            .Distinct()
            .ToList();

        if (providerIds.Count == 0) return new Dictionary<int, string>();

        return (await _providerRepository.GetQueryableAsync())
            .Where(p => providerIds.Contains(p.Id))
            .ToDictionary(p => p.Id, p => p.ProviderName);
    }

    private async Task<Dictionary<int, string>> BuildMaterialDictAsync(List<Waybill> waybills)
    {
        var materialIds = waybills
            .Where(w => w.MaterialId.HasValue)
            .Select(w => w.MaterialId!.Value)
            .Distinct()
            .ToList();

        if (materialIds.Count == 0) return new Dictionary<int, string>();

        return (await _materialRepository.GetQueryableAsync())
            .Where(m => materialIds.Contains(m.Id))
            .ToDictionary(m => m.Id, m => m.Name);
    }

    private static StandardExportRow MapToExportRow(
        Waybill waybill,
        Dictionary<int, string> providerDict,
        Dictionary<int, string> materialDict)
    {
        var providerName = waybill.ProviderId.HasValue &&
                           providerDict.TryGetValue(waybill.ProviderId.Value, out var pn)
            ? pn
            : string.Empty;

        var materialName = waybill.MaterialId.HasValue &&
                           materialDict.TryGetValue(waybill.MaterialId.Value, out var mn)
            ? mn
            : string.Empty;

        return new StandardExportRow
        {
            PlateNumber = waybill.PlateNumber ?? string.Empty,
            DeliveryType = waybill.DeliveryType switch
            {
                DeliveryType.Receiving => "收料",
                DeliveryType.Sending => "发料",
                _ => string.Empty
            },
            MaterialName = materialName,
            OrderType = waybill.OrderType switch
            {
                OrderTypeEnum.FirstWeight => "首称中",
                OrderTypeEnum.Completed => "已完成",
                OrderTypeEnum.Esc => "已取消",
                _ => string.Empty
            },
            PlanQuantity = waybill.OrderPlanOnPcs,
            PlanWeight = waybill.OrderPlanOnWeight,
            OffsetCount = waybill.OffsetCount,
            ActualQuantity = waybill.OrderPcs,
            ActualWeight = waybill.OrderGoodsWeight,
            UnitConversion = waybill.MaterialUnitRate,
            JoinTime = waybill.JoinTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty,
            OutTime = waybill.OutTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty,
            ProviderName = providerName,
            OrderNo = waybill.OrderNo ?? string.Empty,
            Remark = waybill.Remark ?? string.Empty
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
