using System.Collections.ObjectModel;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Events;
using MaterialClient.Common.Services;
using MaterialClient.UI;
using MaterialClient.UI.Models;
using MaterialClient.UI.Services;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;

namespace MaterialClient.Urban.ViewModels;

/// <summary>
///     Urban attended weighing ViewModel
///     Subscribes to weighing pipeline events via ILocalEventBus to drive UI updates
///     Uses Common.Entities.WeighingRecord directly (no local duplicate model)
/// </summary>
public class UrbanAttendedWeighingViewModel : ReactiveObject, IDisposable, ITransientDependency
{
    private readonly ILocalEventBus _localEventBus;
    private readonly IRepository<WeighingRecord, long> _weighingRecordRepository;
    private readonly IAttendedWeighingService _attendedWeighingService;
    private readonly SharedDeviceStatusTracker _deviceStatusTracker;
    private readonly ILogger<UrbanAttendedWeighingViewModel> _logger;
    private readonly CompositeDisposable _subscriptions = [];

    private const int PageSize = 20;

    public UrbanAttendedWeighingViewModel(
        ILocalEventBus localEventBus,
        IRepository<WeighingRecord, long> weighingRecordRepository,
        IAttendedWeighingService attendedWeighingService,
        SharedDeviceStatusTracker deviceStatusTracker,
        ILogger<UrbanAttendedWeighingViewModel> logger)
    {
        _localEventBus = localEventBus;
        _weighingRecordRepository = weighingRecordRepository;
        _attendedWeighingService = attendedWeighingService;
        _deviceStatusTracker = deviceStatusTracker;
        _logger = logger;
    }

    /// <summary>
    ///     Initialize event subscriptions (call in UI-thread-safe context)
    /// </summary>
    public void Initialize()
    {
        // Subscribe to WeighingRecordCreatedEventData to refresh list
        _subscriptions.Add(
            _localEventBus
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
                }));

        // Subscribe to StatusChangedEventData to update status text
        _subscriptions.Add(
            _localEventBus
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
                }));

        // Subscribe to ActiveTab changes to trigger record reload
        _subscriptions.Add(
            this.WhenAnyValue(x => x.ActiveTab)
                .Skip(1) // Skip initial value
                .Subscribe(tabName =>
                {
                    CurrentPage = 1;
                    _ = ReloadRecordsAsync();
                }));

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
    [Reactive]
    public ObservableCollection<WeighingRecord> WeighingRecords { get; set; } = [];

    /// <summary>
    ///     Device status list (using shared DeviceStatusItem from MaterialClient.UI)
    /// </summary>
    [Reactive]
    public ObservableCollection<DeviceStatusItem> DeviceStatuses { get; set; } =
        new(DeviceStatusCatalog.BuildItems(false, false, false, false, false));

    /// <summary>
    ///     Currently selected weighing record
    /// </summary>
    [Reactive]
    public WeighingRecord? SelectedRecord { get; set; }

    /// <summary>
    ///     Current weight display
    /// </summary>
    [Reactive]
    public string CurrentWeight { get; set; } = "0.00";

    /// <summary>
    ///     Weight status text
    /// </summary>
    [Reactive]
    public string WeightStatus { get; set; } = "等待上磅";

    /// <summary>
    ///     Weight status color
    /// </summary>
    [Reactive]
    public string WeightStatusColor { get; set; } = "#94A3B8";

    /// <summary>
    ///     Currently active tab (All/Normal/Abnormal)
    /// </summary>
    [Reactive]
    public string ActiveTab { get; set; } = "全部";

    /// <summary>
    ///     Search keyword (plate number fuzzy query)
    /// </summary>
    [Reactive]
    public string SearchText { get; set; } = "";

    /// <summary>
    ///     Query start time
    /// </summary>
    [Reactive]
    public DateTime? StartTime { get; set; }

    /// <summary>
    ///     Query end time
    /// </summary>
    [Reactive]
    public DateTime? EndTime { get; set; }

    /// <summary>
    ///     Current page number
    /// </summary>
    [Reactive]
    public int CurrentPage { get; set; } = 1;

    /// <summary>
    ///     Total page count
    /// </summary>
    [Reactive]
    public int TotalPages { get; set; } = 1;

    /// <summary>
    ///     Total record count
    /// </summary>
    [Reactive]
    public int TotalCount { get; set; }

    #endregion

    #region Public Methods

    /// <summary>
    ///     Tab filter: switch filter tab
    /// </summary>
    public void SetFilterTab(string tab)
    {
        ActiveTab = tab;
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
    ///     Start device status polling (same catalog as MaterialClient main app).
    /// </summary>
    public void StartDeviceStatusMonitoring()
    {
        if (_deviceStatusTrackerStarted) return;

        _deviceStatusTracker.StatusesChanged += OnDeviceStatusesChanged;
        OnDeviceStatusesChanged(_deviceStatusTracker.GetCurrentStatuses());
        _deviceStatusTracker.StartMonitoring();
        _deviceStatusTrackerStarted = true;
    }

    private bool _deviceStatusTrackerStarted;

    private void OnDeviceStatusesChanged(DeviceStatusItem[] items)
    {
        RxApp.MainThreadScheduler.Schedule(() =>
        {
            DeviceStatuses.Clear();
            foreach (var item in items)
            {
                DeviceStatuses.Add(item);
            }
        });
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
        _deviceStatusTracker.StatusesChanged -= OnDeviceStatusesChanged;
        _deviceStatusTracker.Dispose();
        _subscriptions.Dispose();
    }
}
