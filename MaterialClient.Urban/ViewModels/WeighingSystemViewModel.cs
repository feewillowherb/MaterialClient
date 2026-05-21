using System.Collections.ObjectModel;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Events;
using MaterialClient.Common.Services;
using MaterialClient.Common.Services.AttendedWeighing;
using MaterialClient.Urban.Models;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;

namespace MaterialClient.Urban.ViewModels;

/// <summary>
///     称重系统主界面 ViewModel
///     通过 ILocalEventBus 订阅称重管线事件，驱动 UI 更新
/// </summary>
public class WeighingSystemViewModel : ReactiveObject, IDisposable
{
    private readonly ILocalEventBus _localEventBus;
    private readonly IRepository<WeighingRecord, long> _weighingRecordRepository;
    private readonly IAttendedWeighingService _attendedWeighingService;
    private readonly ILogger<WeighingSystemViewModel> _logger;
    private readonly List<IDisposable> _subscriptions = [];

    private ObservableCollection<WeighingRecord> _weighingRecords = [];
    private ObservableCollection<DeviceStatus> _deviceStatuses = [];
    private WeighingRecord? _selectedRecord;
    private string _currentWeight = "0.00";
    private string _weightStatus = "等待上磅";
    private string _weightStatusColor = "#94A3B8";

    // Filter / search state
    private string _activeTab = "全部";
    private string _searchText = "";
    private DateTime? _startTime;
    private DateTime? _endTime;
    private int _currentPage = 1;
    private int _totalPages = 1;
    private int _totalCount;

    private const int PageSize = 20;

    public WeighingSystemViewModel(
        ILocalEventBus localEventBus,
        IRepository<WeighingRecord, long> weighingRecordRepository,
        IAttendedWeighingService attendedWeighingService,
        ILogger<WeighingSystemViewModel> logger)
    {
        _localEventBus = localEventBus;
        _weighingRecordRepository = weighingRecordRepository;
        _attendedWeighingService = attendedWeighingService;
        _logger = logger;
    }

    /// <summary>
    ///     初始化事件订阅（在 UI 线程安全的上下文中调用）
    /// </summary>
    public void Initialize()
    {
        // 3.1 订阅 WeighingRecordCreatedEventData 刷新列表
        var recordCreatedSub = _localEventBus
            .Subscribe<WeighingRecordCreatedEventData>(async eventData =>
            {
                try
                {
                    await ReloadRecordsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "处理 WeighingRecordCreatedEventData 失败");
                }
            });
        _subscriptions.Add(recordCreatedSub);

        // 3.2 订阅 StatusChangedEventData 更新状态文案
        var statusChangedSub = _localEventBus
            .Subscribe<StatusChangedEventData>(async eventData =>
            {
                try
                {
                    UpdateStatusDisplay(eventData.Status);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "处理 StatusChangedEventData 失败");
                }
            });
        _subscriptions.Add(statusChangedSub);

        // 3.3 订阅重量更新（通过 IAttendedWeighingService 的状态机间接获取）
        // CurrentWeight 通过设备回调在 AttendedWeighingService 内部更新
        // 这里通过 ILocalEventBus 订阅 PlateNumberChangedEventData 等事件来同步
        _logger.LogInformation("WeighingSystemViewModel 事件订阅初始化完成");
    }

    /// <summary>
    ///     设置当前重量（由外部调用，如从设备回调中更新）
    /// </summary>
    public void UpdateCurrentWeight(decimal weight)
    {
        RxApp.MainThreadScheduler.Schedule(() =>
        {
            CurrentWeight = weight.ToString("N0");
        });
    }

    #region Properties

    /// <summary>
    ///     称重记录列表
    /// </summary>
    public ObservableCollection<WeighingRecord> WeighingRecords
    {
        get => _weighingRecords;
        set => this.RaiseAndSetIfChanged(ref _weighingRecords, value);
    }

    /// <summary>
    ///     设备状态列表
    /// </summary>
    public ObservableCollection<DeviceStatus> DeviceStatuses
    {
        get => _deviceStatuses;
        set => this.RaiseAndSetIfChanged(ref _deviceStatuses, value);
    }

    /// <summary>
    ///     当前选中的称重记录
    /// </summary>
    public WeighingRecord? SelectedRecord
    {
        get => _selectedRecord;
        set => this.RaiseAndSetIfChanged(ref _selectedRecord, value);
    }

    /// <summary>
    ///     当前重量显示
    /// </summary>
    public string CurrentWeight
    {
        get => _currentWeight;
        set => this.RaiseAndSetIfChanged(ref _currentWeight, value);
    }

    /// <summary>
    ///     称重状态文本
    /// </summary>
    public string WeightStatus
    {
        get => _weightStatus;
        set => this.RaiseAndSetIfChanged(ref _weightStatus, value);
    }

    /// <summary>
    ///     称重状态颜色
    /// </summary>
    public string WeightStatusColor
    {
        get => _weightStatusColor;
        set => this.RaiseAndSetIfChanged(ref _weightStatusColor, value);
    }

    /// <summary>
    ///     当前激活的 Tab（全部/正常/异常）
    /// </summary>
    public string ActiveTab
    {
        get => _activeTab;
        set
        {
            if (this.RaiseAndSetIfChanged(ref _activeTab, value))
            {
                _ = ReloadRecordsAsync();
            }
        }
    }

    /// <summary>
    ///     搜索关键词（车牌号模糊查询）
    /// </summary>
    public string SearchText
    {
        get => _searchText;
        set => this.RaiseAndSetIfChanged(ref _searchText, value);
    }

    /// <summary>
    ///     查询开始时间
    /// </summary>
    public DateTime? StartTime
    {
        get => _startTime;
        set => this.RaiseAndSetIfChanged(ref _startTime, value);
    }

    /// <summary>
    ///     查询结束时间
    /// </summary>
    public DateTime? EndTime
    {
        get => _endTime;
        set => this.RaiseAndSetIfChanged(ref _endTime, value);
    }

    /// <summary>
    ///     当前页码
    /// </summary>
    public int CurrentPage
    {
        get => _currentPage;
        set => this.RaiseAndSetIfChanged(ref _currentPage, value);
    }

    /// <summary>
    ///     总页数
    /// </summary>
    public int TotalPages
    {
        get => _totalPages;
        set => this.RaiseAndSetIfChanged(ref _totalPages, value);
    }

    /// <summary>
    ///     总记录数
    /// </summary>
    public int TotalCount
    {
        get => _totalCount;
        set => this.RaiseAndSetIfChanged(ref _totalCount, value);
    }

    #endregion

    #region Public Methods

    /// <summary>
    ///     3.4 Tab 筛选：切换筛选 Tab
    /// </summary>
    public void SetFilterTab(string tab)
    {
        ActiveTab = tab;
        CurrentPage = 1;
        _ = ReloadRecordsAsync();
    }

    /// <summary>
    ///     3.5 搜索：执行搜索
    /// </summary>
    public void Search()
    {
        CurrentPage = 1;
        _ = ReloadRecordsAsync();
    }

    /// <summary>
    ///     3.6 分页：上一页
    /// </summary>
    public void PreviousPage()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            _ = ReloadRecordsAsync();
        }
    }

    /// <summary>
    ///     3.6 分页：下一页
    /// </summary>
    public void NextPage()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            _ = ReloadRecordsAsync();
        }
    }

    /// <summary>
    ///     加载设备状态（占位，后续接入真实设备状态）
    /// </summary>
    public void LoadDeviceStatuses()
    {
        DeviceStatuses =
        [
            new() { DeviceName = "地磅设备", IsOnline = true },
            new() { DeviceName = "摄像头", IsOnline = true },
            new() { DeviceName = "车牌识别", IsOnline = false },
        ];
    }

    #endregion

    #region Private Methods

    /// <summary>
    ///     从本地仓储重新加载称重记录（带筛选、搜索、分页）
    /// </summary>
    private async Task ReloadRecordsAsync()
    {
        try
        {
            var query = (await _weighingRecordRepository.GetQueryableAsync())
                .Where(r => r.WeighingMode == WeighingMode.UrbanMode);

            // 3.4 Tab 筛选
            query = ActiveTab switch
            {
                "正常" => query.Where(r => r.SyncStatus != SyncStatus.Failed),
                "异常" => query.Where(r => r.SyncStatus == SyncStatus.Failed),
                _ => query // "全部"
            };

            // 3.5 搜索：车牌号模糊查询
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                query = query.Where(r => r.PlateNumber != null && r.PlateNumber.Contains(SearchText));
            }

            // 3.5 搜索：时间范围查询
            if (StartTime.HasValue)
            {
                query = query.Where(r => r.AddDate >= StartTime.Value);
            }

            if (EndTime.HasValue)
            {
                query = query.Where(r => r.AddDate <= EndTime.Value);
            }

            // 计算总数
            var totalCount = query.Count();
            TotalCount = totalCount;
            TotalPages = totalCount > 0 ? (int)Math.Ceiling((double)totalCount / PageSize) : 1;

            // 3.6 分页查询
            var records = query
                .OrderByDescending(r => r.AddDate)
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            // 在 UI 线程更新集合
            RxApp.MainThreadScheduler.Schedule(() =>
            {
                WeighingRecords = new ObservableCollection<WeighingRecord>(records);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重新加载称重记录失败");
        }
    }

    /// <summary>
    ///     3.2 根据称重状态更新状态文案和颜色
    /// </summary>
    private void UpdateStatusDisplay(AttendedWeighingStatus status)
    {
        RxApp.MainThreadScheduler.Schedule(() =>
        {
            switch (status)
            {
                case AttendedWeighingStatus.OffScale:
                    WeightStatus = "等待上磅";
                    WeightStatusColor = "#94A3B8";
                    break;
                case AttendedWeighingStatus.WaitingForStability:
                    WeightStatus = "正在称重";
                    WeightStatusColor = "#FBBF24";
                    break;
                case AttendedWeighingStatus.WeightStabilized:
                case AttendedWeighingStatus.WaitingForDeparture:
                    WeightStatus = "称重已结束";
                    WeightStatusColor = "#4ADE80";
                    break;
            }
        });
    }

    #endregion

    public void Dispose()
    {
        foreach (var subscription in _subscriptions)
        {
            try
            {
                subscription.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "释放事件订阅失败");
            }
        }

        _subscriptions.Clear();
    }
}
