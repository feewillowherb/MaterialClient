using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Services.Hardware;
using MaterialClient.Common.Services.Hikvision;
using ReactiveUI;
using Volo.Abp.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialClient.Common.Services;

namespace MaterialClient.ViewModels;

public partial class AttendedWeighingViewModel : ViewModelBase, IDisposable
{
    private readonly IRepository<WeighingRecord, long> _weighingRecordRepository;
    private readonly IRepository<Waybill, long> _waybillRepository;
    private readonly IServiceProvider _serviceProvider;
    private readonly ITruckScaleWeightService _truckScaleWeightService;
    private readonly MaterialClient.Common.Services.IAttendedWeighingService? _attendedWeighingService;
    private readonly CompositeDisposable _disposables = new();

    [ObservableProperty] private ObservableCollection<WeighingRecord> _unmatchedWeighingRecords = new();

    [ObservableProperty] private ObservableCollection<Waybill> _completedWaybills = new();

    [ObservableProperty] private WeighingRecord? _selectedWeighingRecord;

    [ObservableProperty] private Waybill? _selectedWaybill;

    [ObservableProperty] private ObservableCollection<string> _vehiclePhotos = new();

    [ObservableProperty] private string? _billPhotoPath;

    [ObservableProperty] private DateTime _currentTime = DateTime.Now;

    [ObservableProperty] private decimal _currentWeight;

    [ObservableProperty] private bool _isReceiving = true;

    [ObservableProperty] private bool _isShowAllRecords = true;

    [ObservableProperty] private bool _isShowUnmatched;

    [ObservableProperty] private bool _isShowCompleted;

    [ObservableProperty] private ObservableCollection<object> _displayRecords = new();

    [ObservableProperty] private object? _selectedRecord;

    [ObservableProperty] private string? _currentEntryPhoto1;

    [ObservableProperty] private string? _currentEntryPhoto2;

    [ObservableProperty] private string? _currentEntryPhoto3;

    [ObservableProperty] private string? _currentEntryPhoto4;

    [ObservableProperty] private string? _entryPhoto1;

    [ObservableProperty] private string? _entryPhoto2;

    [ObservableProperty] private string? _entryPhoto3;

    [ObservableProperty] private string? _entryPhoto4;

    [ObservableProperty] private string? _exitPhoto1;

    [ObservableProperty] private string? _exitPhoto2;

    [ObservableProperty] private string? _exitPhoto3;

    [ObservableProperty] private string? _exitPhoto4;

    [ObservableProperty] private string? _materialInfo;

    [ObservableProperty] private string? _offsetInfo;

    [ObservableProperty] private bool _isScaleOnline;

    [ObservableProperty] private bool _isCameraOnline;

    [ObservableProperty] private ObservableCollection<CameraStatusViewModel> _cameraStatuses = new();

    /// <summary>
    /// 是否有摄像头状态数据
    /// </summary>
    public bool HasCameraStatuses => CameraStatuses.Count > 0;

    [ObservableProperty] private string? _mostFrequentPlateNumber;

    /// <summary>
    /// 当前显示的视图（true=主视图，false=详情视图）
    /// </summary>
    [ObservableProperty] private bool _isShowingMainView = true;

    /// <summary>
    /// 是否显示详情视图（IsShowingMainView 的反转）
    /// </summary>
    public bool IsShowingDetailView => !IsShowingMainView;

    /// <summary>
    /// 当前要显示详情的称重记录
    /// </summary>
    [ObservableProperty] private WeighingRecord? _currentWeighingRecordForDetail;

    /// <summary>
    /// 详情视图的 ViewModel
    /// </summary>
    [ObservableProperty] private AttendedWeighingDetailViewModel? _detailViewModel;

    // 分页相关属性
    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _pageSize = 20;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private int _totalPages;
    [ObservableProperty] private ObservableCollection<WeighingRecord> _pagedUnmatchedWeighingRecords = new();
    [ObservableProperty] private ObservableCollection<Waybill> _pagedCompletedWaybills = new();

    private AttendedWeighingStatus _currentWeighingStatus = AttendedWeighingStatus.OffScale;

    /// <summary>
    /// 当前称重状态的中文文本
    /// </summary>
    public string CurrentWeighingStatusText => GetStatusText(_currentWeighingStatus);

    public bool IsWeighingRecordSelected => SelectedWeighingRecord != null && SelectedWaybill == null;
    public bool IsWaybillSelected => SelectedWaybill != null && SelectedWeighingRecord == null;

    /// <summary>
    /// 页码信息文本
    /// </summary>
    public string PageInfoText => $"第 {CurrentPage} / {TotalPages} 页";

    public AttendedWeighingViewModel(
        IRepository<WeighingRecord, long> weighingRecordRepository,
        IRepository<Waybill, long> waybillRepository,
        IServiceProvider serviceProvider,
        ITruckScaleWeightService truckScaleWeightService,
        IAttendedWeighingService attendedWeighingService
    )
    {
        _weighingRecordRepository = weighingRecordRepository;
        _waybillRepository = waybillRepository;
        _serviceProvider = serviceProvider;
        _truckScaleWeightService = truckScaleWeightService;
        _attendedWeighingService = attendedWeighingService;

        // Subscribe to CameraStatuses collection changes
        CameraStatuses.CollectionChanged += (s, e) => OnPropertyChanged(nameof(HasCameraStatuses));

        // Commands are now generated by [RelayCommand] attributes

        // Subscribe to SelectedWeighingRecord changes
        this.WhenAnyValue(x => x.SelectedWeighingRecord)
            .Subscribe(HandleSelectedWeighingRecordChanged);

        // Subscribe to SelectedWaybill changes
        this.WhenAnyValue(x => x.SelectedWaybill)
            .Subscribe(HandleSelectedWaybillChanged);

        // Subscribe to display mode changes to update IsWeighingRecordSelected and IsWaybillSelected
        this.WhenAnyValue(
                x => x.SelectedWeighingRecord,
                x => x.SelectedWaybill,
                (record, waybill) => (record, waybill))
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(IsWeighingRecordSelected));
                this.RaisePropertyChanged(nameof(IsWaybillSelected));
            });

        // Load initial data
        _ = RefreshAsync();

        StartTimeUpdateTimer();

        // Subscribe to weight updates from truck scale
        _truckScaleWeightService.WeightUpdates
            .DistinctUntilChanged()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(weight => { CurrentWeight = weight; })
            .DisposeWith(_disposables);

        // Start timer to check scale online status periodically
        StartScaleStatusCheckTimer();

        // Start timer to check camera online status periodically
        StartCameraStatusCheckTimer();

        // Start all devices when ViewModel is created
        _ = StartAllDevicesAsync();

        // Start ReactiveUI observable to update most frequent plate number
        StartPlateNumberObservable();

        // Start ReactiveUI observable to update weighing status
        StartStatusObservable();
    }

    /// <summary>
    /// Start all devices
    /// </summary>
    private async Task StartAllDevicesAsync()
    {
        try
        {
            var deviceManagerService =
                _serviceProvider.GetRequiredService<IDeviceManagerService>();
            await deviceManagerService.StartAsync();

            var attendedWeighingService =
                _serviceProvider.GetRequiredService<IAttendedWeighingService>();
            await attendedWeighingService.StartAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error starting devices: {ex.Message}");
        }
    }

    /// <summary>
    /// Start timer to periodically check scale online status
    /// </summary>
    private void StartScaleStatusCheckTimer()
    {
        var statusTimer = new Timer(_ =>
        {
            try
            {
                // Timer callback runs on thread pool, check status on background thread
                var isOnline = _truckScaleWeightService.IsOnline;

                // Update property on UI thread to avoid blocking
                Dispatcher.UIThread.Post(() => { IsScaleOnline = isOnline; });
            }
            catch
            {
                // Update property on UI thread
                Dispatcher.UIThread.Post(() => { IsScaleOnline = false; });
            }
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(2)); // Check every 2 seconds

        _disposables.Add(statusTimer);
    }

    /// <summary>
    /// Start timer to periodically check camera online status
    /// </summary>
    private void StartCameraStatusCheckTimer()
    {
        var statusTimer = new Timer(_ =>
        {
            // Timer callback runs on thread pool, execute async check without blocking
            _ = Task.Run(async () =>
            {
                try
                {
                    await CheckCameraOnlineStatusAsync();
                }
                catch
                {
                    // Update property on UI thread
                    Dispatcher.UIThread.Post(() => { IsCameraOnline = false; });
                }
            });
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(5)); // Check every 5 seconds

        _disposables.Add(statusTimer);
    }

    /// <summary>
    /// Check camera online status
    /// </summary>
    private async Task CheckCameraOnlineStatusAsync()
    {
        try
        {
            var settingsService =
                _serviceProvider.GetRequiredService<MaterialClient.Common.Services.ISettingsService>();
            var hikvisionService = _serviceProvider.GetRequiredService<IHikvisionService>();
            var settings = await settingsService.GetSettingsAsync();
            var cameraConfigs = settings.CameraConfigs;

            if (cameraConfigs.Count == 0)
            {
                // Update property on UI thread
                Dispatcher.UIThread.Post(() =>
                {
                    IsCameraOnline = false;
                    CameraStatuses.Clear();
                });
                return;
            }

            // Check each camera's online status
            var cameraStatusList = new List<CameraStatusViewModel>();
            bool anyOnline = false;

            foreach (var cameraConfig in cameraConfigs)
            {
                var cameraStatus = new CameraStatusViewModel
                {
                    Name = cameraConfig.Name,
                    Ip = cameraConfig.Ip,
                    Port = cameraConfig.Port
                };

                if (string.IsNullOrWhiteSpace(cameraConfig.Ip) ||
                    string.IsNullOrWhiteSpace(cameraConfig.Port) ||
                    string.IsNullOrWhiteSpace(cameraConfig.UserName) ||
                    string.IsNullOrWhiteSpace(cameraConfig.Password))
                {
                    cameraStatus.IsOnline = false;
                    cameraStatusList.Add(cameraStatus);
                    continue;
                }

                if (!int.TryParse(cameraConfig.Port, out var port))
                {
                    cameraStatus.IsOnline = false;
                    cameraStatusList.Add(cameraStatus);
                    continue;
                }

                var hikvisionConfig = new HikvisionDeviceConfig
                {
                    Ip = cameraConfig.Ip,
                    Port = port,
                    Username = cameraConfig.UserName,
                    Password = cameraConfig.Password
                };

                var isOnline = await Task.Run(() => hikvisionService.IsOnline(hikvisionConfig));
                cameraStatus.IsOnline = isOnline;
                cameraStatusList.Add(cameraStatus);

                if (isOnline)
                {
                    anyOnline = true;
                }
            }

            // Update properties on UI thread
            Dispatcher.UIThread.Post(() =>
            {
                IsCameraOnline = anyOnline;
                CameraStatuses.Clear();
                foreach (var status in cameraStatusList)
                {
                    CameraStatuses.Add(status);
                }
            });
        }
        catch
        {
            // Update property on UI thread
            Dispatcher.UIThread.Post(() =>
            {
                IsCameraOnline = false;
                CameraStatuses.Clear();
            });
        }
    }


    /// <summary>
    /// Cleanup resources
    /// </summary>
    public void Dispose()
    {
        _disposables.Dispose();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            // Load unmatched weighing records (RecordType == Unmatch)
            var allRecords = await _weighingRecordRepository.GetListAsync();
            var unmatchedRecords = allRecords
                .Where(x => x.MatchedId == null)
                .OrderByDescending(r => r.CreationTime)
                .ToList();

            UnmatchedWeighingRecords.Clear();
            foreach (var record in unmatchedRecords)
            {
                UnmatchedWeighingRecords.Add(record);
            }

            // Load completed waybills
            var allWaybills = await _waybillRepository.GetListAsync();
            var waybills = allWaybills
                .OrderByDescending(w => w.CreationTime)
                .ToList();

            CompletedWaybills.Clear();
            foreach (var waybill in waybills)
            {
                CompletedWaybills.Add(waybill);
            }

            // 应用分页
            ApplyPaging();
        }
        catch
        {
            // If repositories are not available, collections will remain empty
            // This allows the UI to work even before ABP is fully initialized
        }
    }

    /// <summary>
    /// 应用分页逻辑
    /// </summary>
    private void ApplyPaging()
    {
        if (IsShowAllRecords)
        {
            // 全部记录模式：合并未完成和已完成记录进行分页
            var combinedTotal = UnmatchedWeighingRecords.Count + CompletedWaybills.Count;
            TotalCount = combinedTotal;
            TotalPages = (int)Math.Ceiling(combinedTotal / (double)PageSize);
            if (TotalPages == 0) TotalPages = 1;

            // 确保当前页在有效范围内
            if (CurrentPage > TotalPages) CurrentPage = TotalPages;
            if (CurrentPage < 1) CurrentPage = 1;

            // 计算当前页应该显示哪些记录
            var skip = (CurrentPage - 1) * PageSize;
            var unmatchedCount = UnmatchedWeighingRecords.Count;

            if (skip < unmatchedCount)
            {
                // 当前页包含未完成记录
                var unmatchedToShow = UnmatchedWeighingRecords.Skip(skip).Take(PageSize).ToList();
                var remaining = PageSize - unmatchedToShow.Count;

                PagedUnmatchedWeighingRecords.Clear();
                foreach (var record in unmatchedToShow)
                {
                    PagedUnmatchedWeighingRecords.Add(record);
                }

                // 如果还有剩余空间，添加已完成记录
                if (remaining > 0)
                {
                    var waybillsToShow = CompletedWaybills.Take(remaining).ToList();
                    PagedCompletedWaybills.Clear();
                    foreach (var waybill in waybillsToShow)
                    {
                        PagedCompletedWaybills.Add(waybill);
                    }
                }
                else
                {
                    PagedCompletedWaybills.Clear();
                }
            }
            else
            {
                // 当前页只包含已完成记录
                PagedUnmatchedWeighingRecords.Clear();
                var waybillSkip = skip - unmatchedCount;
                var waybillsToShow = CompletedWaybills.Skip(waybillSkip).Take(PageSize).ToList();
                PagedCompletedWaybills.Clear();
                foreach (var waybill in waybillsToShow)
                {
                    PagedCompletedWaybills.Add(waybill);
                }
            }
        }
        else if (IsShowUnmatched)
        {
            // 只显示未完成记录
            TotalCount = UnmatchedWeighingRecords.Count;
            TotalPages = (int)Math.Ceiling(TotalCount / (double)PageSize);
            if (TotalPages == 0) TotalPages = 1;

            // 确保当前页在有效范围内
            if (CurrentPage > TotalPages) CurrentPage = TotalPages;
            if (CurrentPage < 1) CurrentPage = 1;

            var skip = (CurrentPage - 1) * PageSize;
            var pagedRecords = UnmatchedWeighingRecords.Skip(skip).Take(PageSize).ToList();

            PagedUnmatchedWeighingRecords.Clear();
            foreach (var record in pagedRecords)
            {
                PagedUnmatchedWeighingRecords.Add(record);
            }

            PagedCompletedWaybills.Clear();
        }
        else if (IsShowCompleted)
        {
            // 只显示已完成记录
            TotalCount = CompletedWaybills.Count;
            TotalPages = (int)Math.Ceiling(TotalCount / (double)PageSize);
            if (TotalPages == 0) TotalPages = 1;

            // 确保当前页在有效范围内
            if (CurrentPage > TotalPages) CurrentPage = TotalPages;
            if (CurrentPage < 1) CurrentPage = 1;

            var skip = (CurrentPage - 1) * PageSize;
            var pagedWaybills = CompletedWaybills.Skip(skip).Take(PageSize).ToList();

            PagedUnmatchedWeighingRecords.Clear();
            PagedCompletedWaybills.Clear();
            foreach (var waybill in pagedWaybills)
            {
                PagedCompletedWaybills.Add(waybill);
            }
        }

        // Update display records after pagination
        UpdateDisplayRecords();
    }

    [RelayCommand]
    private void SetReceiving()
    {
        IsReceiving = true;
    }

    [RelayCommand]
    private void SetSending()
    {
        IsReceiving = false;
    }

    [RelayCommand]
    private void ShowAllRecords()
    {
        SetDisplayMode(0);
    }

    [RelayCommand]
    private void ShowUnmatched()
    {
        SetDisplayMode(1);
    }

    [RelayCommand]
    private void ShowCompleted()
    {
        SetDisplayMode(2);
    }

    [RelayCommand]
    private void SelectRecord(object? record)
    {
        SelectedRecord = record;
        // TODO: Load photos for the selected record
    }

    /// <summary>
    /// 打开称重记录详情视图
    /// </summary>
    [RelayCommand]
    private void OpenDetail(object? record)
    {
        if (record is WeighingRecord weighingRecord)
        {
            try
            {
                // 设置选中的记录，用于高亮显示
                SelectedWeighingRecord = weighingRecord;
                CurrentWeighingRecordForDetail = weighingRecord;

                // 手动创建 ViewModel，传入 WeighingRecord
                var weighingRecordRepository = _serviceProvider.GetRequiredService<IRepository<WeighingRecord, long>>();
                var materialRepository = _serviceProvider.GetRequiredService<IRepository<Material, int>>();
                var providerRepository = _serviceProvider.GetRequiredService<IRepository<Provider, int>>();
                var materialUnitRepository = _serviceProvider.GetRequiredService<IRepository<MaterialUnit, int>>();

                DetailViewModel = new AttendedWeighingDetailViewModel(
                    weighingRecord,
                    weighingRecordRepository,
                    materialRepository,
                    providerRepository,
                    materialUnitRepository,
                    _serviceProvider
                );

                // 订阅保存/废单完成事件以刷新列表并返回主视图
                DetailViewModel.SaveCompleted += OnDetailSaveCompleted;
                DetailViewModel.AbolishCompleted += OnDetailAbolishCompleted;
                DetailViewModel.CloseRequested += OnDetailCloseRequested;

                // 切换到详情视图
                IsShowingMainView = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"打开详情视图失败: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 返回主视图
    /// </summary>
    [RelayCommand]
    private void BackToMain()
    {
        // 取消订阅事件
        if (DetailViewModel != null)
        {
            DetailViewModel.SaveCompleted -= OnDetailSaveCompleted;
            DetailViewModel.AbolishCompleted -= OnDetailAbolishCompleted;
            DetailViewModel.CloseRequested -= OnDetailCloseRequested;
        }

        // 清除选中状态
        SelectedWeighingRecord = null;

        IsShowingMainView = true;
        CurrentWeighingRecordForDetail = null;
        DetailViewModel = null;
    }

    private async void OnDetailSaveCompleted(object? sender, EventArgs e)
    {
        await RefreshAsync();
        BackToMain();
    }

    private async void OnDetailAbolishCompleted(object? sender, EventArgs e)
    {
        await RefreshAsync();
        BackToMain();
    }

    private void OnDetailCloseRequested(object? sender, EventArgs e)
    {
        BackToMain();
    }

    [RelayCommand]
    private void TakeBillPhoto()
    {
        // TODO: Implement bill photo capture
    }

    [RelayCommand]
    private void Save()
    {
        // TODO: Implement save logic
    }

    [RelayCommand]
    private void Close()
    {
        // TODO: Implement close logic
    }

    [RelayCommand]
    private void OpenSettings()
    {
        try
        {
            var settingsWindow = _serviceProvider.GetRequiredService<Views.SettingsWindow>();
            settingsWindow.Show();
        }
        catch
        {
            // Handle error opening settings window
        }
    }

    private void SetDisplayMode(int mode)
    {
        IsShowAllRecords = mode == 0;
        IsShowUnmatched = mode == 1;
        IsShowCompleted = mode == 2;
        OnPropertyChanged(nameof(IsShowAllRecords));
        OnPropertyChanged(nameof(ShowUnmatched));
        OnPropertyChanged(nameof(ShowCompleted));
        CurrentPage = 1; // 切换显示模式时重置到第一页
        ApplyPaging();
    }

    /// <summary>
    /// 上一页命令
    /// </summary>
    [RelayCommand]
    private void GoToPreviousPage()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            ApplyPaging();
        }
    }

    private bool CanGoToPreviousPage() => CurrentPage > 1;

    /// <summary>
    /// 下一页命令
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanGoToNextPage))]
    private void GoToNextPage()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            ApplyPaging();
        }
    }

    private bool CanGoToNextPage() => CurrentPage < TotalPages;

    /// <summary>
    /// 跳转到指定页
    /// </summary>
    [RelayCommand]
    private void GoToPage(int page)
    {
        if (page >= 1 && page <= TotalPages)
        {
            CurrentPage = page;
            ApplyPaging();
        }
    }

    // 属性变更处理，更新命令可用性和计算属性
    partial void OnCurrentPageChanged(int value)
    {
        GoToPreviousPageCommand.NotifyCanExecuteChanged();
        GoToNextPageCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(PageInfoText));
    }

    partial void OnTotalPagesChanged(int value)
    {
        GoToPreviousPageCommand.NotifyCanExecuteChanged();
        GoToNextPageCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(PageInfoText));
    }

    partial void OnCameraStatusesChanged(ObservableCollection<CameraStatusViewModel> value)
    {
        OnPropertyChanged(nameof(HasCameraStatuses));
    }

    partial void OnIsShowingMainViewChanged(bool value)
    {
        OnPropertyChanged(nameof(IsShowingDetailView));
    }

    private void UpdateDisplayRecords()
    {
        DisplayRecords.Clear();

        if (IsShowAllRecords)
        {
            foreach (var record in PagedUnmatchedWeighingRecords)
            {
                DisplayRecords.Add(record);
            }

            foreach (var waybill in PagedCompletedWaybills)
            {
                DisplayRecords.Add(waybill);
            }
        }
        else if (IsShowUnmatched)
        {
            foreach (var record in PagedUnmatchedWeighingRecords)
            {
                DisplayRecords.Add(record);
            }
        }
        else if (IsShowCompleted)
        {
            foreach (var waybill in PagedCompletedWaybills)
            {
                DisplayRecords.Add(waybill);
            }
        }
    }

    private void StartTimeUpdateTimer()
    {
        var timeTimer = new Timer(_ => CurrentTime = DateTime.Now, null,
            TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// Start ReactiveUI observable to update most frequent plate number
    /// </summary>
    private void StartPlateNumberObservable()
    {
        if (_attendedWeighingService == null)
        {
            return;
        }

        // Subscribe to plate number changes from service
        _attendedWeighingService.MostFrequentPlateNumberChanges
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(plateNumber => { MostFrequentPlateNumber = plateNumber; })
            .DisposeWith(_disposables);
    }

    /// <summary>
    /// Start ReactiveUI observable to update weighing status
    /// </summary>
    private void StartStatusObservable()
    {
        if (_attendedWeighingService == null)
        {
            return;
        }

        // Subscribe to status changes from service
        _attendedWeighingService.StatusChanges
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(status =>
            {
                _currentWeighingStatus = status;
                OnPropertyChanged(nameof(CurrentWeighingStatusText));
            })
            .DisposeWith(_disposables);
    }

    /// <summary>
    /// 获取状态的中文文本
    /// </summary>
    private static string GetStatusText(AttendedWeighingStatus status)
    {
        return status switch
        {
            AttendedWeighingStatus.OffScale => "称重已结束",
            AttendedWeighingStatus.WaitingForStability => "等待稳定",
            AttendedWeighingStatus.WeightStabilized => "重量已稳定",
            _ => "未知状态"
        };
    }

    private void HandleSelectedWeighingRecordChanged(WeighingRecord? value)
    {
        // Clear waybill selection when weighing record is selected
        if (value != null)
        {
            SelectedWaybill = null;
            _ = LoadWeighingRecordPhotos(value);
        }
        else
        {
            VehiclePhotos.Clear();
            BillPhotoPath = null;
        }

        this.RaisePropertyChanged(nameof(IsWeighingRecordSelected));
        this.RaisePropertyChanged(nameof(IsWaybillSelected));
    }

    private void HandleSelectedWaybillChanged(Waybill? value)
    {
        // Clear weighing record selection when waybill is selected
        if (value != null)
        {
            SelectedWeighingRecord = null;
            _ = LoadWaybillPhotos(value);
        }
        else
        {
            VehiclePhotos.Clear();
            BillPhotoPath = null;
        }

        this.RaisePropertyChanged(nameof(IsWeighingRecordSelected));
        this.RaisePropertyChanged(nameof(IsWaybillSelected));
    }

    private async Task LoadWeighingRecordPhotos(WeighingRecord record)
    {
        try
        {
            var attachmentRepository = _serviceProvider.GetService<IRepository<WeighingRecordAttachment, int>>();
            if (attachmentRepository != null)
            {
                var attachments = await attachmentRepository.GetListAsync(
                    predicate: x => x.WeighingRecordId == record.Id,
                    includeDetails: true
                );

                VehiclePhotos.Clear();
                BillPhotoPath = null;

                foreach (var attachment in attachments)
                {
                    if (attachment.AttachmentFile != null && !string.IsNullOrEmpty(attachment.AttachmentFile.LocalPath))
                    {
                        // Determine if attachment is vehicle photo or bill photo based on AttachType
                        if (attachment.AttachmentFile.AttachType == AttachType.EntryPhoto ||
                            attachment.AttachmentFile.AttachType == AttachType.ExitPhoto)
                        {
                            VehiclePhotos.Add(attachment.AttachmentFile.LocalPath);
                        }
                        else if (attachment.AttachmentFile.AttachType == AttachType.TicketPhoto)
                        {
                            BillPhotoPath = attachment.AttachmentFile.LocalPath;
                        }
                    }
                }
            }
        }
        catch
        {
            // If repository is not available, photos will remain empty
        }
    }

    private async Task LoadWaybillPhotos(Waybill waybill)
    {
        try
        {
            var waybillAttachmentRepository = _serviceProvider.GetService<IRepository<WaybillAttachment, int>>();
            if (waybillAttachmentRepository != null)
            {
                var attachments = await waybillAttachmentRepository.GetListAsync(
                    predicate: x => x.WaybillId == waybill.Id,
                    includeDetails: true
                );

                VehiclePhotos.Clear();
                BillPhotoPath = null;

                foreach (var attachment in attachments)
                {
                    if (attachment.AttachmentFile != null && !string.IsNullOrEmpty(attachment.AttachmentFile.LocalPath))
                    {
                        // Determine if attachment is vehicle photo or bill photo based on AttachType
                        if (attachment.AttachmentFile.AttachType == AttachType.EntryPhoto ||
                            attachment.AttachmentFile.AttachType == AttachType.ExitPhoto)
                        {
                            VehiclePhotos.Add(attachment.AttachmentFile.LocalPath);
                        }
                        else if (attachment.AttachmentFile.AttachType == AttachType.TicketPhoto)
                        {
                            BillPhotoPath = attachment.AttachmentFile.LocalPath;
                        }
                    }
                }
            }
        }
        catch
        {
            // If repository is not available, photos will remain empty
        }
    }
}