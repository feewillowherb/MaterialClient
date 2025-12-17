using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Models;
using MaterialClient.Common.Services;
using MaterialClient.Common.Services.Hardware;
using MaterialClient.Common.Services.Hikvision;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using MaterialClient.Views;

namespace MaterialClient.ViewModels;

public partial class AttendedWeighingViewModel : ViewModelBase, IDisposable
{
    private readonly IWeighingMatchingService _weighingMatchingService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ITruckScaleWeightService _truckScaleWeightService;
    private readonly IAttendedWeighingService? _attendedWeighingService;
    private readonly CompositeDisposable _disposables = new();
    private AttendedWeighingStatus _currentWeighingStatus = AttendedWeighingStatus.OffScale;

    #region Properties

    [Reactive] private ObservableCollection<WeighingListItemDto> _listItems = new();

    [Reactive] private ObservableCollection<WeighingListItemDto> _pagedListItems = new();

    [Reactive] private WeighingListItemDto? _selectedListItem;

    [Reactive] private ObservableCollection<string> _vehiclePhotos = new();

    [Reactive] private string? _billPhotoPath;

    [Reactive] private DateTime _currentTime = DateTime.Now;

    [Reactive] private decimal _currentWeight;

    [Reactive] private bool _isReceiving = true;

    [Reactive] private bool _isShowAllRecords = true;

    [Reactive] private bool _isShowUnmatched;

    [Reactive] private bool _isShowCompleted;

    [Reactive] private PhotoGridViewModel? _photoGridViewModel;

    [Reactive] private string? _materialInfo;

    [Reactive] private string? _offsetInfo;

    [Reactive] private string? _joinWeightInfo;

    [Reactive] private string? _outWeightInfo;

    [Reactive] private bool _isScaleOnline;

    [Reactive] private bool _isCameraOnline;

    [Reactive] private ObservableCollection<CameraStatusViewModel> _cameraStatuses = new();

    public bool HasCameraStatuses => CameraStatuses.Count > 0;

    [Reactive] private string? _mostFrequentPlateNumber;

    [Reactive] private bool _isShowingMainView = true;

    public bool IsShowingDetailView => !IsShowingMainView;

    [Reactive] private AttendedWeighingDetailViewModel? _detailViewModel;

    [Reactive] private int _currentPage = 1;

    [Reactive] private int _pageSize = 10;

    [Reactive] private int _totalCount;

    [Reactive] private int _totalPages;

    [Reactive] private DateTime? _searchStartDate;

    [Reactive] private DateTime? _searchEndDate;

    [Reactive] private string? _searchPlateNumber;

    public string CurrentWeighingStatusText => GetStatusText(_currentWeighingStatus);
    public bool IsCompletedWaybillSelected => SelectedListItem is { ItemType: WeighingListItemType.Waybill, OrderType: OrderTypeEnum.Completed };
    public string PageInfoText => $"第 {CurrentPage} / {TotalPages} 页";
    public bool IsSending => !IsReceiving;

    #endregion

    public AttendedWeighingViewModel(
        IWeighingMatchingService weighingMatchingService,
        IServiceProvider serviceProvider,
        ITruckScaleWeightService truckScaleWeightService,
        IAttendedWeighingService attendedWeighingService
    )
    {
        _weighingMatchingService = weighingMatchingService;
        _serviceProvider = serviceProvider;
        _truckScaleWeightService = truckScaleWeightService;
        _attendedWeighingService = attendedWeighingService;

        PhotoGridViewModel = new PhotoGridViewModel(serviceProvider);

        // Setup property change notifications
        this.WhenAnyValue(x => x.SelectedListItem)
            .Subscribe(async item =>
            {
                this.RaisePropertyChanged(nameof(IsCompletedWaybillSelected));
                
                if (item != null)
                {
                    await LoadListItemPhotos(item);
                    UpdateDisplayInfoFromListItem(item);
                }
                else
                {
                    VehiclePhotos.Clear();
                    BillPhotoPath = null;
                    PhotoGridViewModel?.Clear();
                    ClearDisplayInfo();
                }
            })
            .DisposeWith(_disposables);

        this.WhenAnyValue(x => x.CameraStatuses.Count)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(HasCameraStatuses)))
            .DisposeWith(_disposables);

        this.WhenAnyValue(x => x.IsShowingMainView)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(IsShowingDetailView)))
            .DisposeWith(_disposables);

        this.WhenAnyValue(x => x.CurrentPage, x => x.TotalPages)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(PageInfoText)))
            .DisposeWith(_disposables);

        this.WhenAnyValue(x => x.IsReceiving)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(IsSending)))
            .DisposeWith(_disposables);

        _ = RefreshAsync();
        StartTimeUpdateTimer();

        _truckScaleWeightService.WeightUpdates
            .DistinctUntilChanged()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(weight => { CurrentWeight = weight; })
            .DisposeWith(_disposables);

        StartScaleStatusCheckTimer();
        StartCameraStatusCheckTimer();
        _ = StartAllDevicesAsync();
        StartPlateNumberObservable();
        StartStatusObservable();
        StartWeighingRecordCreatedObservable();
        StartDeliveryTypeObservable();
    }

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

    private void StartScaleStatusCheckTimer()
    {
        var statusTimer = new Timer(_ =>
        {
            try
            {
                var isOnline = _truckScaleWeightService.IsOnline;
                Dispatcher.UIThread.Post(() => { IsScaleOnline = isOnline; });
            }
            catch
            {
                Dispatcher.UIThread.Post(() => { IsScaleOnline = false; });
            }
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(2));

        _disposables.Add(statusTimer);
    }

    private void StartCameraStatusCheckTimer()
    {
        var cameraStatusTimer = new Timer(_ =>
        {
            Task.Run(async () =>
            {
                try
                {
                    await CheckCameraOnlineStatusAsync();
                }
                catch
                {
                    Dispatcher.UIThread.Post(() => { IsCameraOnline = false; });
                }
            });
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));

        _disposables.Add(cameraStatusTimer);
    }

    private async Task CheckCameraOnlineStatusAsync()
    {
        try
        {
            var settingsService =
                _serviceProvider.GetRequiredService<ISettingsService>();
            var hikvisionService = _serviceProvider.GetRequiredService<IHikvisionService>();
            var settings = await settingsService.GetSettingsAsync();
            var cameraConfigs = settings.CameraConfigs;

            if (cameraConfigs.Count == 0)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    IsCameraOnline = false;
                    CameraStatuses.Clear();
                });
                return;
            }

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
            Dispatcher.UIThread.Post(() =>
            {
                IsCameraOnline = false;
                CameraStatuses.Clear();
            });
        }
    }

    public void Dispose()
    {
        _disposables.Dispose();
    }

    #region Command Implementations

    [ReactiveCommand]
    private async Task RefreshAsync()
    {
        try
        {
            bool? isCompleted = null;
            if (IsShowUnmatched)
            {
                isCompleted = false;
            }
            else if (IsShowCompleted)
            {
                isCompleted = true;
            }

            // 获取所有数据（不分页），以便应用搜索过滤
            var input = new GetWeighingListItemsInput
            {
                IsCompleted = isCompleted,
                SkipCount = 0,
                MaxResultCount = 10000 // 获取足够多的数据以支持搜索过滤
            };

            var result = await _weighingMatchingService.GetListItemsAsync(input);

            // 应用搜索过滤
            var filteredItems = result.Items.AsEnumerable();
            
            // 按日期范围过滤
            if (SearchStartDate.HasValue)
            {
                var startDate = SearchStartDate.Value.Date;
                filteredItems = filteredItems.Where(item => item.JoinTime.Date >= startDate);
            }
            
            if (SearchEndDate.HasValue)
            {
                var endDate = SearchEndDate.Value.Date.AddDays(1); // 包含结束日期当天
                filteredItems = filteredItems.Where(item => item.JoinTime.Date < endDate);
            }
            
            // 按车牌号过滤
            if (!string.IsNullOrWhiteSpace(SearchPlateNumber))
            {
                var plateNumber = SearchPlateNumber.Trim();
                filteredItems = filteredItems.Where(item => 
                    !string.IsNullOrEmpty(item.PlateNumber) && 
                    item.PlateNumber.Contains(plateNumber, StringComparison.OrdinalIgnoreCase));
            }

            var filteredList = filteredItems.ToList();

            // 计算分页
            TotalCount = filteredList.Count;
            TotalPages = (int)Math.Ceiling(TotalCount / (double)PageSize);
            if (TotalPages == 0) TotalPages = 1;

            if (CurrentPage > TotalPages) CurrentPage = TotalPages;
            if (CurrentPage < 1) CurrentPage = 1;

            // 应用分页
            var pagedItems = filteredList
                .OrderByDescending(item => item.JoinTime)
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            ListItems.Clear();
            PagedListItems.Clear();
            foreach (var item in pagedItems)
            {
                ListItems.Add(item);
                PagedListItems.Add(item);
            }
        }
        catch
        {
            // If service is not available, collections will remain empty
        }
    }

    [ReactiveCommand]
    private void SetReceiving()
    {
        _attendedWeighingService?.SetDeliveryType(DeliveryType.Receiving);
    }

    [ReactiveCommand]
    private void SetSending()
    {
        _attendedWeighingService?.SetDeliveryType(DeliveryType.Sending);
    }

    [ReactiveCommand]
    private void ShowAllRecords()
    {
        SetDisplayMode(0);
    }

    [ReactiveCommand]
    private void ShowUnmatched()
    {
        SetDisplayMode(1);
    }

    [ReactiveCommand]
    private void ShowCompleted()
    {
        SetDisplayMode(2);
    }

    [ReactiveCommand]
    private void SelectListItem(WeighingListItemDto? item)
    {
        if (item == null) return;

        SelectedListItem = item;

        if (item is { ItemType: WeighingListItemType.Waybill, OrderType: OrderTypeEnum.Completed })
        {
            SelectCompletedWaybill(item);
        }
        else
        {
            _ = OpenDetail(item);
        }
    }

    private void SelectCompletedWaybill(WeighingListItemDto _)
    {
        // 直接使用 DTO 中的预计算字段，无需再次查询数据库
        // SelectedListItem 的变化会自动触发 UpdateDisplayInfoFromListItem
        IsShowingMainView = true;
    }

    /// <summary>
    /// 从列表项更新显示信息（使用预计算字段）
    /// </summary>
    private void UpdateDisplayInfoFromListItem(WeighingListItemDto item)
    {
        // 使用预计算的供应商名称和物料信息
        MaterialInfo = item.MaterialInfo;
        OffsetInfo = item.OffsetInfo;

        // 使用预计算的进出场重量
        if (item.JoinWeight.HasValue)
        {
            JoinWeightInfo = $"{item.JoinWeight.Value:F2} 吨 {item.JoinTime:HH:mm:ss}";
        }
        else
        {
            JoinWeightInfo = null;
        }

        if (item.OutWeight.HasValue && item.OutTime.HasValue)
        {
            OutWeightInfo = $"{item.OutWeight.Value:F2} 吨 {item.OutTime.Value:HH:mm:ss}";
        }
        else
        {
            OutWeightInfo = null;
        }
    }

    /// <summary>
    /// 清空显示信息
    /// </summary>
    private void ClearDisplayInfo()
    {
        MaterialInfo = null;
        OffsetInfo = null;
        JoinWeightInfo = null;
        OutWeightInfo = null;
    }

    [ReactiveCommand]
    private Task OpenDetail(WeighingListItemDto? item)
    {
        if (item == null) return Task.CompletedTask;

        try
        {
            DetailViewModel = new AttendedWeighingDetailViewModel(
                item,
                _serviceProvider
            );

            DetailViewModel.SaveCompleted += OnDetailSaveCompleted;
            DetailViewModel.AbolishCompleted += OnDetailAbolishCompleted;
            DetailViewModel.CloseRequested += OnDetailCloseRequested;
            DetailViewModel.MatchCompleted += OnDetailMatchCompleted;
            DetailViewModel.CompleteCompleted += OnDetailCompleteCompleted;

            IsShowingMainView = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"打开详情视图失败: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    [ReactiveCommand]
    private void BackToMain()
    {
        if (DetailViewModel != null)
        {
            DetailViewModel.SaveCompleted -= OnDetailSaveCompleted;
            DetailViewModel.AbolishCompleted -= OnDetailAbolishCompleted;
            DetailViewModel.CloseRequested -= OnDetailCloseRequested;
            DetailViewModel.MatchCompleted -= OnDetailMatchCompleted;
            DetailViewModel.CompleteCompleted -= OnDetailCompleteCompleted;
        }

        SelectedListItem = null;
        IsShowingMainView = true;
        DetailViewModel = null;
    }

    private async void OnDetailSaveCompleted(object? sender, EventArgs e)
    {
        await RefreshAsync();
        await SelectLatestCompletedItemAsync();
        BackToMain();
    }

    private async void OnDetailAbolishCompleted(object? sender, EventArgs e)
    {
        await RefreshAsync();
        await SelectLatestCompletedItemAsync();
        BackToMain();
    }

    private async void OnDetailMatchCompleted(object? sender, EventArgs e)
    {
        await RefreshAsync();
        await SelectLatestCompletedItemAsync();
        BackToMain();
    }

    private async void OnDetailCompleteCompleted(object? sender, EventArgs e)
    {
        await RefreshAsync();
        await SelectLatestCompletedItemAsync();
        BackToMain();
    }

    private async void OnDetailCloseRequested(object? sender, EventArgs e)
    {
        await RefreshAsync();
        await SelectLatestCompletedItemAsync();
        BackToMain();
    }

    /// <summary>
    /// 选择已完成的第一个数据
    /// </summary>
    private async Task SelectLatestCompletedItemAsync()
    {
        try
        {
            // 从当前列表中查找第一个完成数据
            var firstCompleted = ListItems
                .FirstOrDefault(item => item.OrderType == OrderTypeEnum.Completed);

            if (firstCompleted != null)
            {
                // 如果当前页有完成数据，直接选择
                SelectedListItem = firstCompleted;
            }
            else
            {
                // 如果当前页没有完成数据，切换到显示完成数据模式并刷新
                IsShowCompleted = true;
                IsShowAllRecords = false;
                IsShowUnmatched = false;
                CurrentPage = 1;
                await RefreshAsync();
                
                // 刷新后选择第一条（应该就是已完成的第一个）
                if (ListItems.Count > 0)
                {
                    SelectedListItem = ListItems.FirstOrDefault();
                }
            }
        }
        catch
        {
            // 如果出错，忽略错误，不影响主流程
        }
    }

    [ReactiveCommand]
    private void TakeBillPhoto()
    {
        // TODO: Implement bill photo capture
    }

    [ReactiveCommand]
    private void Save()
    {
        // TODO: Implement save logic
    }

    [ReactiveCommand]
    private void Close()
    {
        // TODO: Implement close logic
    }

    [ReactiveCommand]
    private void OpenSettings()
    {
        try
        {
            var settingsWindow = _serviceProvider.GetRequiredService<SettingsWindow>();
            settingsWindow.Show();
        }
        catch
        {
            // Handle error opening settings window
        }
    }

    [ReactiveCommand]
    private void OpenImageViewer(string? imagePath)
    {
        if (string.IsNullOrEmpty(imagePath))
            return;

        try
        {
            // 先创建并设置 ViewModel
            var viewModel = _serviceProvider.GetRequiredService<ImageViewerViewModel>();
            viewModel.SetImage(imagePath);
            
            // 手动创建窗口，传入已设置的 ViewModel
            var window = new ImageViewerWindow(viewModel);
            window.Show();
        }
        catch
        {
            // Handle error opening image viewer window
        }
    }

    private void SetDisplayMode(int mode)
    {
        IsShowAllRecords = mode == 0;
        IsShowUnmatched = mode == 1;
        IsShowCompleted = mode == 2;
        CurrentPage = 1;
        _ = RefreshAsync();
    }

    [ReactiveCommand]
    private async Task GoToPreviousPageAsync()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            await RefreshAsync();
        }
    }

    [ReactiveCommand]
    private async Task GoToNextPageAsync()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            await RefreshAsync();
        }
    }

    [ReactiveCommand]
    private async Task GoToPageAsync(int page)
    {
        if (page >= 1 && page <= TotalPages)
        {
            CurrentPage = page;
            await RefreshAsync();
        }
    }

    [ReactiveCommand]
    private async Task SearchAsync()
    {
        CurrentPage = 1; // 重置到第一页
        await RefreshAsync();
    }

    [ReactiveCommand]
    private async Task ResetSearchAsync()
    {
        SearchStartDate = null;
        SearchEndDate = null;
        SearchPlateNumber = null;
        CurrentPage = 1; // 重置到第一页
        await RefreshAsync();
    }

    #endregion

    private void StartTimeUpdateTimer()
    {
        var timeTimer = new Timer(_ => CurrentTime = DateTime.Now, null,
            TimeSpan.Zero, TimeSpan.FromSeconds(1));
        _disposables.Add(timeTimer);
    }

    private void StartPlateNumberObservable()
    {
        if (_attendedWeighingService == null)
        {
            return;
        }

        _attendedWeighingService.MostFrequentPlateNumberChanges
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(plateNumber => { MostFrequentPlateNumber = plateNumber; })
            .DisposeWith(_disposables);
    }

    private void StartStatusObservable()
    {
        if (_attendedWeighingService == null)
        {
            return;
        }

        _attendedWeighingService.StatusChanges
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(status =>
            {
                _currentWeighingStatus = status;
                this.RaisePropertyChanged(nameof(CurrentWeighingStatusText));
            })
            .DisposeWith(_disposables);
    }

    private void StartWeighingRecordCreatedObservable()
    {
        if (_attendedWeighingService == null)
        {
            return;
        }

        _attendedWeighingService.WeighingRecordCreated
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(async weighingRecord =>
            {
                System.Diagnostics.Debug.WriteLine(
                    $"AttendedWeighingViewModel: Received new weighing record creation event, ID: {weighingRecord.Id}");
                await RefreshAsync();
            })
            .DisposeWith(_disposables);
    }

    private void StartDeliveryTypeObservable()
    {
        if (_attendedWeighingService == null)
        {
            return;
        }

        // 初始化 IsReceiving 为服务的当前值
        IsReceiving = _attendedWeighingService.CurrentDeliveryType == DeliveryType.Receiving;

        // 订阅 DeliveryType 变化
        _attendedWeighingService.DeliveryTypeChanges
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(deliveryType =>
            {
                IsReceiving = deliveryType == DeliveryType.Receiving;
            })
            .DisposeWith(_disposables);
    }

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

    /// <summary>
    /// 从列表项加载照片（统一接口）
    /// </summary>
    private async Task LoadListItemPhotos(WeighingListItemDto item)
    {
        try
        {
            if (PhotoGridViewModel != null)
            {
                await PhotoGridViewModel.LoadFromListItemAsync(item);
            }

            var attachmentService = _serviceProvider.GetService<IAttachmentService>();
            if (attachmentService != null)
            {
                var attachmentFiles = await attachmentService.GetAttachmentsByListItemAsync(item);

                VehiclePhotos.Clear();
                BillPhotoPath = null;

                foreach (var file in attachmentFiles)
                {
                    if (!string.IsNullOrEmpty(file.LocalPath))
                    {
                        if (file.AttachType == AttachType.EntryPhoto ||
                            file.AttachType == AttachType.ExitPhoto)
                        {
                            VehiclePhotos.Add(file.LocalPath);
                        }
                        else if (file.AttachType == AttachType.TicketPhoto)
                        {
                            BillPhotoPath = file.LocalPath;
                        }
                    }
                }
            }
        }
        catch
        {
            // If service is not available, photos will remain empty
        }
    }
}