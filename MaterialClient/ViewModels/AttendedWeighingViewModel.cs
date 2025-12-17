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
using MaterialClient.Common.Models;
using MaterialClient.Common.Services.Hardware;
using MaterialClient.Common.Services.Hikvision;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Volo.Abp.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using MaterialClient.Common.Services;

namespace MaterialClient.ViewModels;

public partial class AttendedWeighingViewModel : ViewModelBase, IDisposable
{
    private readonly IWeighingMatchingService _weighingMatchingService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ITruckScaleWeightService _truckScaleWeightService;
    private readonly MaterialClient.Common.Services.IAttendedWeighingService? _attendedWeighingService;
    private readonly CompositeDisposable _disposables = new();
    private AttendedWeighingStatus _currentWeighingStatus = AttendedWeighingStatus.OffScale;

    #region Properties

    [Reactive] private ObservableCollection<WeighingListItemDto> _listItems = new();

    [Reactive] private ObservableCollection<WeighingListItemDto> _pagedListItems = new();

    [Reactive] private WeighingRecord? _selectedWeighingRecord;

    [Reactive] private Waybill? _selectedWaybill;

    [Reactive] private string? _selectedWaybillProviderName;

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

    [Reactive] private WeighingRecord? _currentWeighingRecordForDetail;

    [Reactive] private AttendedWeighingDetailViewModel? _detailViewModel;

    [Reactive] private int _currentPage = 1;

    [Reactive] private int _pageSize = 10;

    [Reactive] private int _totalCount;

    [Reactive] private int _totalPages;

    public string CurrentWeighingStatusText => GetStatusText(_currentWeighingStatus);
    public bool IsWeighingRecordSelected => SelectedWeighingRecord != null && SelectedWaybill == null;
    public bool IsWaybillSelected => SelectedWaybill != null && SelectedWeighingRecord == null;
    public string PageInfoText => $"第 {CurrentPage} / {TotalPages} 页";

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
        this.WhenAnyValue(x => x.SelectedWeighingRecord)
            .Subscribe(async value =>
            {
                if (value != null)
                {
                    SelectedWaybill = null;
                    await LoadWeighingRecordPhotos(value);
                }
                else
                {
                    VehiclePhotos.Clear();
                    BillPhotoPath = null;
                    PhotoGridViewModel?.Clear();
                }

                this.RaisePropertyChanged(nameof(IsWeighingRecordSelected));
                this.RaisePropertyChanged(nameof(IsWaybillSelected));
            })
            .DisposeWith(_disposables);

        this.WhenAnyValue(x => x.SelectedWaybill)
            .Subscribe(async value =>
            {
                if (value != null)
                {
                    SelectedWeighingRecord = null;
                    await LoadWaybillPhotos(value);
                }
                else
                {
                    VehiclePhotos.Clear();
                    BillPhotoPath = null;
                    PhotoGridViewModel?.Clear();
                }

                this.RaisePropertyChanged(nameof(IsWeighingRecordSelected));
                this.RaisePropertyChanged(nameof(IsWaybillSelected));
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
        var statusTimer = new Timer(_ =>
        {
            _ = Task.Run(async () =>
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

        _disposables.Add(statusTimer);
    }

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

            var input = new GetWeighingListItemsInput
            {
                IsCompleted = isCompleted,
                SkipCount = (CurrentPage - 1) * PageSize,
                MaxResultCount = PageSize
            };

            var result = await _weighingMatchingService.GetListItemsAsync(input);

            TotalCount = (int)result.TotalCount;
            TotalPages = (int)Math.Ceiling(TotalCount / (double)PageSize);
            if (TotalPages == 0) TotalPages = 1;

            if (CurrentPage > TotalPages) CurrentPage = TotalPages;
            if (CurrentPage < 1) CurrentPage = 1;

            ListItems.Clear();
            PagedListItems.Clear();
            foreach (var item in result.Items)
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
        IsReceiving = true;
    }

    [ReactiveCommand]
    private void SetSending()
    {
        IsReceiving = false;
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

    private async void SelectCompletedWaybill(WeighingListItemDto item)
    {
        if (item.ItemType == WeighingListItemType.Waybill)
        {
            var waybillRepository = _serviceProvider.GetRequiredService<IRepository<Waybill, long>>();
            var waybill = await waybillRepository.GetAsync(item.Id);
            SelectedWaybill = waybill;
            
            // 加载供应商名称
            if (waybill.ProviderId.HasValue)
            {
                var providerRepository = _serviceProvider.GetRequiredService<IRepository<Provider, int>>();
                var provider = await providerRepository.FindAsync(waybill.ProviderId.Value);
                SelectedWaybillProviderName = provider?.ProviderName;
            }
            else
            {
                SelectedWaybillProviderName = null;
            }
            
            // 加载物料信息: {Rate}/{Unit} {MaterialName}
            if (waybill.MaterialId.HasValue)
            {
                var materialRepository = _serviceProvider.GetRequiredService<IRepository<Material, int>>();
                var material = await materialRepository.FindAsync(waybill.MaterialId.Value);
                
                string? unitInfo = null;
                if (waybill.MaterialUnitId.HasValue)
                {
                    var materialUnitRepository = _serviceProvider.GetRequiredService<IRepository<MaterialUnit, int>>();
                    var materialUnit = await materialUnitRepository.FindAsync(waybill.MaterialUnitId.Value);
                    if (materialUnit != null)
                    {
                        unitInfo = $"{materialUnit.Rate}/{materialUnit.UnitName}";
                    }
                }
                
                if (material != null)
                {
                    MaterialInfo = unitInfo != null ? $"{unitInfo} {material.Name}" : material.Name;
                }
                else
                {
                    MaterialInfo = null;
                }
            }
            else
            {
                MaterialInfo = null;
            }
            
            // 加载偏差信息
            if (waybill.OffsetRate.HasValue)
            {
                var offsetRatePercent = waybill.OffsetRate.Value;
                OffsetInfo = $"{offsetRatePercent:F2}%";
            }
            else
            {
                OffsetInfo = null;
            }
            
            // 加载进场重量信息
            var joinWeight = waybill.GetJoinWeight();
            if (joinWeight.HasValue && waybill.JoinTime.HasValue)
            {
                JoinWeightInfo = $"{joinWeight.Value:F2} 吨 {waybill.JoinTime.Value:HH:mm:ss}";
            }
            else
            {
                JoinWeightInfo = null;
            }
            
            // 加载出场重量信息
            var outWeight = waybill.GetOutWeight();
            if (outWeight.HasValue && waybill.OutTime.HasValue)
            {
                OutWeightInfo = $"{outWeight.Value:F2} 吨 {waybill.OutTime.Value:HH:mm:ss}";
            }
            else
            {
                OutWeightInfo = null;
            }
            
            IsShowingMainView = true;
        }
    }

    [ReactiveCommand]
    private async Task OpenDetail(WeighingListItemDto? item)
    {
        try
        {
            var weighingRecordRepository = _serviceProvider.GetRequiredService<IRepository<WeighingRecord, long>>();

            var weighingRecord = await weighingRecordRepository.GetAsync(item.Id);
            SelectedWeighingRecord = weighingRecord;
            CurrentWeighingRecordForDetail = weighingRecord;

            DetailViewModel = new AttendedWeighingDetailViewModel(
                item,
                _serviceProvider
            );

            DetailViewModel.SaveCompleted += OnDetailSaveCompleted;
            DetailViewModel.AbolishCompleted += OnDetailAbolishCompleted;
            DetailViewModel.CloseRequested += OnDetailCloseRequested;
            DetailViewModel.MatchCompleted += OnDetailMatchCompleted;

            IsShowingMainView = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"打开详情视图失败: {ex.Message}");
        }
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
        }

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

    private async void OnDetailMatchCompleted(object? sender, EventArgs e)
    {
        await RefreshAsync();
        BackToMain();
    }

    private void OnDetailCloseRequested(object? sender, EventArgs e)
    {
        BackToMain();
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

    #endregion

    private void StartTimeUpdateTimer()
    {
        var timeTimer = new Timer(_ => CurrentTime = DateTime.Now, null,
            TimeSpan.Zero, TimeSpan.FromSeconds(1));
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

    private async Task LoadWeighingRecordPhotos(WeighingRecord record)
    {
        try
        {
            if (PhotoGridViewModel != null)
            {
                await PhotoGridViewModel.LoadFromWeighingRecordAsync(record);
            }

            var attachmentService = _serviceProvider.GetService<IAttachmentService>();
            if (attachmentService != null)
            {
                var attachmentsDict =
                    await attachmentService.GetAttachmentsByWeighingRecordIdsAsync(new[] { record.Id });

                VehiclePhotos.Clear();
                BillPhotoPath = null;

                if (attachmentsDict.TryGetValue(record.Id, out var attachmentFiles))
                {
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
        }
        catch
        {
            // If service is not available, photos will remain empty
        }
    }

    private async Task LoadWaybillPhotos(Waybill waybill)
    {
        try
        {
            if (PhotoGridViewModel != null)
            {
                await PhotoGridViewModel.LoadFromWaybillAsync(waybill);
            }

            var attachmentService = _serviceProvider.GetService<IAttachmentService>();
            if (attachmentService != null)
            {
                var attachmentsDict = await attachmentService.GetAttachmentsByWaybillIdsAsync(new[] { waybill.Id });

                VehiclePhotos.Clear();
                BillPhotoPath = null;

                if (attachmentsDict.TryGetValue(waybill.Id, out var attachmentFiles))
                {
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
        }
        catch
        {
            // If service is not available, photos will remain empty
        }
    }
}