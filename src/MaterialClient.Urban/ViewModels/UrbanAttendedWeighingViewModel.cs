using System.Collections.ObjectModel;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using MaterialClient.Common.Dtos.Urban;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Events;
using MaterialClient.Common.Providers;
using MaterialClient.Common.Services;
using MaterialClient.Common.Services.AttendedWeighing;
using MaterialClient.Common.Services.Hardware;
using MaterialClient.Common.Services.Urban;
using MaterialClient.UI;
using MaterialClient.UI.Models;
using MaterialClient.UI.Services;
using MaterialClient.UI.ViewModels;
using MaterialClient.UI.Views;
using MaterialClient.Urban.Views.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Ursa.Controls;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Local;

namespace MaterialClient.Urban.ViewModels;

/// <summary>
///     Urban attended weighing ViewModel
///     Subscribes to weighing pipeline events via ILocalEventBus to drive UI updates
/// </summary>
public partial class UrbanAttendedWeighingViewModel : ReactiveObject, IDisposable, ITransientDependency
{
    private readonly ILocalEventBus _localEventBus;
    private readonly IUrbanWeighingExtensionService _urbanWeighingExtensionService;
    private readonly IAttendedWeighingService _attendedWeighingService;
    private readonly ITruckScaleWeightService _truckScaleWeightService;
    private readonly IAttachmentService _attachmentService;
    private readonly SharedDeviceStatusTracker _deviceStatusTracker;
    private readonly ILogger<UrbanAttendedWeighingViewModel> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly CompositeDisposable _subscriptions = [];

    private const int PageSize = 20;

    private const string TabAll = "全部";
    private const string TabNormal = "正常";
    private const string TabAbnormal = "异常";

    public UrbanAttendedWeighingViewModel(
        ILocalEventBus localEventBus,
        IUrbanWeighingExtensionService urbanWeighingExtensionService,
        IAttendedWeighingService attendedWeighingService,
        ITruckScaleWeightService truckScaleWeightService,
        IAttachmentService attachmentService,
        SharedDeviceStatusTracker deviceStatusTracker,
        ILogger<UrbanAttendedWeighingViewModel> logger,
        IServiceProvider serviceProvider)
    {
        _localEventBus = localEventBus;
        _urbanWeighingExtensionService = urbanWeighingExtensionService;
        _attendedWeighingService = attendedWeighingService;
        _truckScaleWeightService = truckScaleWeightService;
        _attachmentService = attachmentService;
        _deviceStatusTracker = deviceStatusTracker;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    ///     Initialize event subscriptions (call in UI-thread-safe context)
    /// </summary>
    public void Initialize()
    {
        MostFrequentPlateNumber = _attendedWeighingService.GetMostFrequentPlateNumber();
        CurrentWeighingStatus = _attendedWeighingService.GetCurrentStatus();
        this.RaisePropertyChanged(nameof(CurrentWeighingStatusText));
        this.RaisePropertyChanged(nameof(IsWeighingActive));

        _subscriptions.Add(
            _localEventBus.Subscribe<PlateNumberChangedEventData>(eventData =>
            {
                RxApp.MainThreadScheduler.Schedule(() => MostFrequentPlateNumber = eventData.PlateNumber);
                return Task.CompletedTask;
            }));

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

        _subscriptions.Add(
            _localEventBus
                .Subscribe<UploadCompletedEventData>(async eventData =>
                {
                    try
                    {
                        await ReloadRecordsAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to handle UploadCompletedEventData");
                    }
                }));

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

        _subscriptions.Add(
            _localEventBus.Subscribe<SettingsSavedEventData>(async _ =>
            {
                try
                {
                    await RefreshDeviceStatusBarAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to refresh device status bar after settings save");
                }
            }));

        _subscriptions.Add(
            this.WhenAnyValue(x => x.SelectedListItem)
                .Subscribe(item => _ = UpdatePhotoPathsAsync(item?.WeighingRecordId)));

        _subscriptions.Add(
            _truckScaleWeightService.WeightUpdates
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(weight =>
                {
                    _logger.LogDebug("Urban UI Weight Update: {Weight}", weight);
                    CurrentWeight = weight;
                }));

        _ = ReloadRecordsAsync();

        _logger.LogInformation("UrbanAttendedWeighingViewModel event subscriptions initialized");
    }

    /// <summary>
    ///     Select a list row and load its photo paths for the sidebar.
    /// </summary>
    [ReactiveCommand]
    private void SelectListItem(UrbanWeighingListItemDto item)
    {
        SelectedListItem = item;
    }

    /// <summary>
    ///     审批称重记录：编辑车牌/重量并更新记录，重置同步状态为 Pending
    /// </summary>
    [ReactiveCommand]
    private async Task ApproveRecordAsync(UrbanWeighingListItemDto? item)
    {
        if (item == null)
        {
            return;
        }

        if (!item.IsAnomaly)
        {
            return;
        }

        try
        {
            var dialogViewModel = new WeighingRecordEditDialogViewModel(
                _serviceProvider.GetRequiredService<IAttachmentService>(),
                _serviceProvider,
                _serviceProvider.GetRequiredService<ILogger<WeighingRecordEditDialogViewModel>>())
            {
                PlateNumber = item.PlateNumber ?? string.Empty,
                TotalWeight = item.TotalWeight.ToString("F2"),
                WeighingDate = item.AddDate.ToString("yyyy-MM-dd HH:mm:ss")
            };

            await dialogViewModel.LoadPhotosAsync(item.WeighingRecordId);

            var dialog = new WeighingRecordEditDialog(dialogViewModel);
            var window = GetWindow();
            var result = await dialog.ShowDialog<EditResult?>(window);

            if (result != null)
            {
                if (string.IsNullOrWhiteSpace(result.PlateNumber))
                {
                    await MessageBox.ShowAsync(window, "车牌号不能为空", "验证错误",
                        MessageBoxIcon.Warning, MessageBoxButton.OK);
                    return;
                }

                if (!PlateNumberValidator.IsValidChinesePlateNumber(result.PlateNumber))
                {
                    await MessageBox.ShowAsync(window, "车牌号不符合规范请修改", "验证错误",
                        MessageBoxIcon.Warning, MessageBoxButton.OK);
                    return;
                }

                var confirmResult = await MessageBox.ShowAsync(window, "确认提交审批修改吗？", "确认审批",
                    MessageBoxIcon.Question, MessageBoxButton.YesNo);
                if (confirmResult != MessageBoxResult.Yes)
                {
                    return;
                }

                // Capture old values before update for edit history tracking
                var oldPlateNumber = item.PlateNumber ?? string.Empty;
                var oldTotalWeight = item.TotalWeight;
                var oldAnomalyReason = item.AnomalyReason ?? string.Empty;

                var weighingRecordService = _serviceProvider.GetRequiredService<IWeighingRecordService>();
                await weighingRecordService.UpdateWeighingRecordAsync(
                    item.WeighingRecordId, result.PlateNumber, result.TotalWeight);

                // Append a single snapshot edit entry with the full post-edit state
                var extension = await _urbanWeighingExtensionService.GetByWeighingRecordIdAsync(item.WeighingRecordId);
                if (extension != null)
                {
                    if (oldPlateNumber != result.PlateNumber || oldTotalWeight != result.TotalWeight)
                    {
                        await _urbanWeighingExtensionService.AppendEditEntryAsync(
                            extension.Id, result.PlateNumber ?? string.Empty, result.TotalWeight, extension.AnomalyReason);
                    }
                }

                await ReloadRecordsAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to approve weighing record {Id}", item.WeighingRecordId);
        }
    }

    private static Window GetWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow
                   ?? throw new InvalidOperationException("Cannot find main window");
        }

        throw new InvalidOperationException("Application is not running in desktop mode");
    }

    /// <summary>
    ///     Set current weight (called from device callback)
    /// </summary>
    public void UpdateCurrentWeight(decimal weight)
    {
        RxApp.MainThreadScheduler.Schedule(() => { CurrentWeight = weight; });
    }

    #region Properties

    [Reactive] private ObservableCollection<UrbanWeighingListItemDto> _listItems = [];

    [Reactive] private ObservableCollection<DeviceStatusItem> _deviceStatuses =
        new(DeviceStatusCatalog.BuildItems(DeviceStatusBarOptions.CoreOnly, false, false, false, false, false));

    [Reactive] private UrbanWeighingListItemDto? _selectedListItem;

    [Reactive] private decimal _currentWeight;

    [Reactive] private AttendedWeighingStatus _currentWeighingStatus = AttendedWeighingStatus.OffScale;

    [Reactive] private string? _mostFrequentPlateNumber;

    [Reactive] private string _activeTab = TabAll;

    public string CurrentWeighingStatusText =>
        AttendedWeighingStatusDisplay.GetStatusText(CurrentWeighingStatus);

    public bool IsWeighingActive => CurrentWeighingStatus != AttendedWeighingStatus.OffScale;

    [Reactive] private string _searchText = "";

    [Reactive] private DateTime? _startTime;

    [Reactive] private DateTime? _endTime;

    [Reactive] private int _currentPage = 1;

    [Reactive] private int _totalPages = 1;

    [Reactive] private int _totalCount;

    [Reactive] private string? _lprPhotoPath;

    [Reactive] private string? _cameraPhotoPath;

    [Reactive] private string _lprPhotoTime = "";

    [Reactive] private string _cameraPhotoTime = "";

    #endregion

    #region Public Methods

    public void SetFilterTab(string tab)
    {
        ActiveTab = NormalizeTabName(tab);
        CurrentPage = 1;
        _ = ReloadRecordsAsync();
    }

    [ReactiveCommand]
    private Task SearchAsync()
    {
        CurrentPage = 1;
        return ReloadRecordsAsync();
    }

    [ReactiveCommand]
    private Task ResetSearchAsync()
    {
        StartTime = null;
        EndTime = null;
        SearchText = string.Empty;
        CurrentPage = 1;
        return ReloadRecordsAsync();
    }

    [ReactiveCommand]
    private Task PreviousPageAsync()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            return ReloadRecordsAsync();
        }

        return Task.CompletedTask;
    }

    [ReactiveCommand]
    private Task NextPageAsync()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            return ReloadRecordsAsync();
        }

        return Task.CompletedTask;
    }

    public void StartDeviceStatusMonitoring()
    {
        if (_deviceStatusTrackerStarted) return;

        _deviceStatusTracker.StatusesChanged += OnDeviceStatusesChanged;
        _ = RefreshDeviceStatusBarAsync();
        _deviceStatusTracker.StartMonitoring();
        _deviceStatusTrackerStarted = true;
    }

    public async Task RefreshDeviceStatusBarAsync()
    {
        await _deviceStatusTracker.RefreshVisibilityFromSettingsAsync();
        OnDeviceStatusesChanged(_deviceStatusTracker.GetCurrentStatuses());
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

    #region Image Viewer Commands

    /// <summary>
    ///     打开车牌识别抓拍图片查看器
    /// </summary>
    [ReactiveCommand]
    private void OpenLprImageViewer(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            var viewModel = _serviceProvider.GetRequiredService<ImageViewerViewModel>();
            viewModel.SetImage(path, "车牌识别抓拍");
            var window = new ImageViewerWindow(viewModel);
            window.Show();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "打开车牌识别图片查看器失败");
        }
    }

    /// <summary>
    ///     打开摄像头抓拍图片查看器
    /// </summary>
    [ReactiveCommand]
    private void OpenCameraImageViewer(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            var viewModel = _serviceProvider.GetRequiredService<ImageViewerViewModel>();
            viewModel.SetImage(path, "摄像头抓拍");
            var window = new ImageViewerWindow(viewModel);
            window.Show();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "打开摄像头图片查看器失败");
        }
    }

    #endregion

    #region Private Methods

    private static string NormalizeTabName(string tab) =>
        tab switch
        {
            TabNormal => TabNormal,
            TabAbnormal => TabAbnormal,
            _ => TabAll
        };

    /// <summary>
    ///     将 UI 标签页映射为查询过滤（全部/全部记录 → null，不过滤 IsAnomaly）
    /// </summary>
    private static string? ToQueryTabFilter(string activeTab) =>
        activeTab switch
        {
            TabNormal => TabNormal,
            TabAbnormal => TabAbnormal,
            _ => null
        };

    private async Task ReloadRecordsAsync()
    {
        try
        {
            var input = new GetUrbanWeighingListInput
            {
                PageIndex = CurrentPage,
                PageSize = PageSize,
                TabFilter = ToQueryTabFilter(ActiveTab),
                SearchText = SearchText,
                StartTime = StartTime,
                EndTime = EndTime
            };

            var result = await _urbanWeighingExtensionService.GetPagedListItemsAsync(input);

            TotalCount = (int)result.TotalCount;
            TotalPages = TotalCount > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 1;

            RxApp.MainThreadScheduler.Schedule(() =>
            {
                ListItems.Clear();
                foreach (var item in result.Items)
                {
                    ListItems.Add(item);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reload weighing records");
        }
    }

    private async Task UpdatePhotoPathsAsync(long? weighingRecordId)
    {
        if (!weighingRecordId.HasValue)
        {
            RxApp.MainThreadScheduler.Schedule(() =>
            {
                LprPhotoPath = null;
                CameraPhotoPath = null;
                LprPhotoTime = "";
                CameraPhotoTime = "";
            });
            return;
        }

        try
        {
            var attachmentsByRecord =
                await _attachmentService.GetAttachmentsByWeighingRecordIdsAsync([weighingRecordId.Value]);

            string? lprPath = null;
            string? cameraPath = null;
            DateTime? lprTime = null;
            DateTime? cameraTime = null;

            if (attachmentsByRecord.TryGetValue(weighingRecordId.Value, out var files))
            {
                foreach (var file in files)
                {
                    if (string.IsNullOrEmpty(file.LocalPath))
                        continue;

                    if (file.AttachType == AttachType.Lrp)
                    {
                        lprPath = file.LocalPath;
                        lprTime = file.AddDate;
                    }
                    else if (file.AttachType == AttachType.UrbanPhoto && cameraPath == null)
                    {
                        cameraPath = file.LocalPath;
                        cameraTime = file.AddDate;
                    }
                }
            }

            RxApp.MainThreadScheduler.Schedule(() =>
            {
                LprPhotoPath = lprPath;
                CameraPhotoPath = cameraPath;
                LprPhotoTime = lprTime.HasValue ? lprTime.Value.ToString("HH:mm:ss") : "";
                CameraPhotoTime = cameraTime.HasValue ? cameraTime.Value.ToString("HH:mm:ss") : "";
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load photo paths for record {RecordId}", weighingRecordId);
            RxApp.MainThreadScheduler.Schedule(() =>
            {
                LprPhotoPath = null;
                CameraPhotoPath = null;
                LprPhotoTime = "";
                CameraPhotoTime = "";
            });
        }
    }

    private void UpdateStatusDisplay(AttendedWeighingStatus status)
    {
        RxApp.MainThreadScheduler.Schedule(() =>
        {
            CurrentWeighingStatus = status;
            this.RaisePropertyChanged(nameof(CurrentWeighingStatusText));
            this.RaisePropertyChanged(nameof(IsWeighingActive));
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
