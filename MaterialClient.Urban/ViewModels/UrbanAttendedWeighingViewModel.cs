using System.Collections.ObjectModel;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Events;
using MaterialClient.Common.Services;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;

namespace MaterialClient.Urban.ViewModels;

/// <summary>
///     Device status display model (inline, replaces deleted Models/DeviceStatus.cs)
/// </summary>
public record DeviceStatusDisplay(string DeviceName, bool IsOnline)
{
    public string StatusText => IsOnline ? "在线" : "离线";
    public string StatusColor => IsOnline ? "#4ADE80" : "#EF4444";
    public string DotColor => IsOnline ? "#4ADE80" : "#EF4444";
}

/// <summary>
///     Urban attended weighing ViewModel
///     Subscribes to weighing pipeline events via ILocalEventBus to drive UI updates
///     Uses Common.Entities.WeighingRecord directly (no local duplicate model)
/// </summary>
public class UrbanAttendedWeighingViewModel : ReactiveObject, IDisposable
{
    private readonly ILocalEventBus _localEventBus;
    private readonly IRepository<WeighingRecord, long> _weighingRecordRepository;
    private readonly IAttendedWeighingService _attendedWeighingService;
    private readonly ILogger<UrbanAttendedWeighingViewModel> _logger;
    private readonly List<IDisposable> _subscriptions = [];

    private ObservableCollection<WeighingRecord> _weighingRecords = [];
    private ObservableCollection<DeviceStatusDisplay> _deviceStatuses = [];
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

    public UrbanAttendedWeighingViewModel(
        ILocalEventBus localEventBus,
        IRepository<WeighingRecord, long> weighingRecordRepository,
        IAttendedWeighingService attendedWeighingService,
        ILogger<UrbanAttendedWeighingViewModel> logger)
    {
        _localEventBus = localEventBus;
        _weighingRecordRepository = weighingRecordRepository;
        _attendedWeighingService = attendedWeighingService;
        _logger = logger;
    }

    /// <summary>
    ///     Initialize event subscriptions (call in UI-thread-safe context)
    /// </summary>
    public void Initialize()
    {
        // Subscribe to WeighingRecordCreatedEventData to refresh list
        var recordCreatedSub = _localEventBus
            .Subscribe<WeighingRecordCreatedEventData>(async eventData =>
            {
                try
                {
                    await ReloadRecordsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to handle WeighingRecordCreatedEventData");
                }
            });
        _subscriptions.Add(recordCreatedSub);

        // Subscribe to StatusChangedEventData to update status text
        var statusChangedSub = _localEventBus
            .Subscribe<StatusChangedEventData>(async eventData =>
            {
                try
                {
                    UpdateStatusDisplay(eventData.Status);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to handle StatusChangedEventData");
                }
            });
        _subscriptions.Add(statusChangedSub);

        _logger.LogInformation("UrbanAttendedWeighingViewModel event subscriptions initialized");
    }

    /// <summary>
    ///     Set current weight (called from device callback)
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
    ///     Weighing records list (using Common.Entities.WeighingRecord)
    /// </summary>
    public ObservableCollection<WeighingRecord> WeighingRecords
    {
        get => _weighingRecords;
        set => this.RaiseAndSetIfChanged(ref _weighingRecords, value);
    }

    /// <summary>
    ///     Device status list
    /// </summary>
    public ObservableCollection<DeviceStatusDisplay> DeviceStatuses
    {
        get => _deviceStatuses;
        set => this.RaiseAndSetIfChanged(ref _deviceStatuses, value);
    }

    /// <summary>
    ///     Currently selected weighing record
    /// </summary>
    public WeighingRecord? SelectedRecord
    {
        get => _selectedRecord;
        set => this.RaiseAndSetIfChanged(ref _selectedRecord, value);
    }

    /// <summary>
    ///     Current weight display
    /// </summary>
    public string CurrentWeight
    {
        get => _currentWeight;
        set => this.RaiseAndSetIfChanged(ref _currentWeight, value);
    }

    /// <summary>
    ///     Weight status text
    /// </summary>
    public string WeightStatus
    {
        get => _weightStatus;
        set => this.RaiseAndSetIfChanged(ref _weightStatus, value);
    }

    /// <summary>
    ///     Weight status color
    /// </summary>
    public string WeightStatusColor
    {
        get => _weightStatusColor;
        set => this.RaiseAndSetIfChanged(ref _weightStatusColor, value);
    }

    /// <summary>
    ///     Currently active tab (All/Normal/Abnormal)
    /// </summary>
    public string ActiveTab
    {
        get => _activeTab;
        set
        {
            var changed = _activeTab != value;
            this.RaiseAndSetIfChanged(ref _activeTab, value);
            if (changed)
            {
                _ = ReloadRecordsAsync();
            }
        }
    }

    /// <summary>
    ///     Search keyword (plate number fuzzy query)
    /// </summary>
    public string SearchText
    {
        get => _searchText;
        set => this.RaiseAndSetIfChanged(ref _searchText, value);
    }

    /// <summary>
    ///     Query start time
    /// </summary>
    public DateTime? StartTime
    {
        get => _startTime;
        set => this.RaiseAndSetIfChanged(ref _startTime, value);
    }

    /// <summary>
    ///     Query end time
    /// </summary>
    public DateTime? EndTime
    {
        get => _endTime;
        set => this.RaiseAndSetIfChanged(ref _endTime, value);
    }

    /// <summary>
    ///     Current page number
    /// </summary>
    public int CurrentPage
    {
        get => _currentPage;
        set => this.RaiseAndSetIfChanged(ref _currentPage, value);
    }

    /// <summary>
    ///     Total page count
    /// </summary>
    public int TotalPages
    {
        get => _totalPages;
        set => this.RaiseAndSetIfChanged(ref _totalPages, value);
    }

    /// <summary>
    ///     Total record count
    /// </summary>
    public int TotalCount
    {
        get => _totalCount;
        set => this.RaiseAndSetIfChanged(ref _totalCount, value);
    }

    #endregion

    #region Public Methods

    /// <summary>
    ///     Tab filter: switch filter tab
    /// </summary>
    public void SetFilterTab(string tab)
    {
        ActiveTab = tab;
        CurrentPage = 1;
        _ = ReloadRecordsAsync();
    }

    /// <summary>
    ///     Search: execute search
    /// </summary>
    public void Search()
    {
        CurrentPage = 1;
        _ = ReloadRecordsAsync();
    }

    /// <summary>
    ///     Pagination: previous page
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
    ///     Pagination: next page
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
    ///     Load device statuses (placeholder for real device status)
    /// </summary>
    public void LoadDeviceStatuses()
    {
        DeviceStatuses =
        [
            new("地磅设备", true),
            new("摄像头", true),
            new("车牌识别", false),
        ];
    }

    #endregion

    #region Private Methods

    /// <summary>
    ///     Reload weighing records from local repository (with filter, search, pagination)
    /// </summary>
    private async Task ReloadRecordsAsync()
    {
        try
        {
            var query = (await _weighingRecordRepository.GetQueryableAsync())
                .Where(r => r.WeighingMode == WeighingMode.UrbanMode);

            // Tab filter
            query = ActiveTab switch
            {
                "正常" => query.Where(r => r.SyncStatus != SyncStatus.Failed),
                "异常" => query.Where(r => r.SyncStatus == SyncStatus.Failed),
                _ => query // "全部"
            };

            // Search: plate number fuzzy query
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                query = query.Where(r => r.PlateNumber != null && r.PlateNumber.Contains(SearchText));
            }

            // Search: time range query
            if (StartTime.HasValue)
            {
                query = query.Where(r => r.AddDate >= StartTime.Value);
            }

            if (EndTime.HasValue)
            {
                query = query.Where(r => r.AddDate <= EndTime.Value);
            }

            // Calculate total count
            var totalCount = query.Count();
            TotalCount = totalCount;
            TotalPages = totalCount > 0 ? (int)Math.Ceiling((double)totalCount / PageSize) : 1;

            // Paginated query
            var records = query
                .OrderByDescending(r => r.AddDate)
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            // Update collection on UI thread
            RxApp.MainThreadScheduler.Schedule(() =>
            {
                WeighingRecords = new ObservableCollection<WeighingRecord>(records);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reload weighing records");
        }
    }

    /// <summary>
    ///     Update status text and color based on weighing status
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
                _logger.LogWarning(ex, "Failed to dispose event subscription");
            }
        }

        _subscriptions.Clear();
    }
}
