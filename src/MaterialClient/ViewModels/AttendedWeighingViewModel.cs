using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Notifications;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using MaterialClient.Common.Configuration;
using MaterialClient.UI;
using MaterialClient.UI.Controls;
using MaterialClient.UI.Models;
using MaterialClient.UI.ViewModels;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Events;
using MaterialClient.Common.Services.Authentication;
using MaterialClient.Common.Models;
using MaterialClient.Common.Services;
using MaterialClient.Common.Services.AttendedWeighing;
using MaterialClient.Common.Services.Hardware;
using MaterialClient.Common.Services.Hikvision;
using MaterialClient.Common.Utils;
using MaterialClient.UI.Views;
using MaterialClient.Views;
using MaterialClient.Views.AttendedWeighing;
using MaterialClient.Views.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ursa.Controls;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Local;

namespace MaterialClient.ViewModels;

public partial class AttendedWeighingViewModel : ViewModelBase, IDisposable, ITransientDependency
{
    private readonly IAttendedWeighingService? _attendedWeighingService;
    private readonly IAuthenticationService _authenticationService;
    private readonly CompositeDisposable _disposables = new();
    private readonly IServiceProvider _serviceProvider;
    private readonly ITruckScaleWeightService _truckScaleWeightService;
    private readonly IWeighingMatchingService _weighingMatchingService;
    private readonly ISoundDeviceService _soundDeviceService;
    private readonly ISettingsService _settingsService;
    private readonly ILprDeviceOnlineStatusService _lprDeviceOnlineStatusService;
    private readonly ISyncMaterialService _syncMaterialService;
    private readonly IAttachmentService _attachmentService;
    private readonly ILocalEventBus _localEventBus;
    private AttendedWeighingStatus _currentWeighingStatus = AttendedWeighingStatus.OffScale;
    private bool _isSyncing;
    private DispatcherTimer? _notificationFadeOutTimer;
    private readonly TextBlock _notificationTextBlockHolder = new();
    private readonly BehaviorSubject<int> _soundDeviceStatus = new(-1);
    private IDisposable? _statusPollingDisposable;

    public AttendedWeighingViewModel(
        IWeighingMatchingService weighingMatchingService,
        IServiceProvider serviceProvider,
        ITruckScaleWeightService truckScaleWeightService,
        IAttendedWeighingService attendedWeighingService,
        IAuthenticationService authenticationService,
        ISoundDeviceService soundDeviceService,
        ISettingsService settingsService,
        ILprDeviceOnlineStatusService lprDeviceOnlineStatusService,
        ISyncMaterialService syncMaterialService,
        IAttachmentService attachmentService,
        ILocalEventBus localEventBus
    ) : base(serviceProvider.GetService<ILogger<AttendedWeighingViewModel>>())
    {
        _weighingMatchingService = weighingMatchingService;
        _serviceProvider = serviceProvider;
        _truckScaleWeightService = truckScaleWeightService;
        _attendedWeighingService = attendedWeighingService;
        _authenticationService = authenticationService;
        _soundDeviceService = soundDeviceService;
        _settingsService = settingsService;
        _lprDeviceOnlineStatusService = lprDeviceOnlineStatusService;
        _syncMaterialService = syncMaterialService;
        _attachmentService = attachmentService;
        _localEventBus = localEventBus;

        PhotoGridViewModel = new PhotoGridViewModel(serviceProvider);

        // Setup device status sync for shared DeviceStatusBar
        this.WhenAnyValue(
                x => x.IsScaleOnline,
                x => x.IsCameraOnline,
                x => x.IsUsbCameraOnline,
                x => x.IsPrinterOnline,
                x => x.IsLprOnline)
            .Subscribe(_ => SyncDeviceStatuses())
            .DisposeWith(_disposables);

        this.WhenAnyValue(
                x => x.IsSoundDeviceOnline,
                x => x.DocumentCameraEnabled,
                x => x.IsPrinterEnabled,
                x => x.IsSoundDeviceEnabled)
            .Subscribe(_ => SyncDeviceStatuses())
            .DisposeWith(_disposables);

        // Setup property change notifications
        this.WhenAnyValue(x => x.SelectedListItem)
            .Subscribe(async item =>
            {
                this.RaisePropertyChanged(nameof(IsCompletedWaybillSelected));
                this.RaisePropertyChanged(nameof(CanPrintSolidWaste));

                if (item != null)
                {
                    await LoadListItemPhotos(item);
                    UpdateDisplayInfoFromListItem(item);

                    // 更新预览状态
                    this.RaisePropertyChanged(nameof(HasBillPhoto));
                    this.RaisePropertyChanged(nameof(BillPhotoButtonText));
                    this.RaisePropertyChanged(nameof(ShouldShowPreview));

                    // 根据ShouldShowPreview决定是否启动预览
                    if (DocumentCameraEnabled && IsUsbCameraOnline && ShouldShowPreview)
                        _ = StartUsbCameraPreviewAsync();
                    else
                        _ = StopUsbCameraPreviewAsync();
                }
                else
                {
                    VehiclePhotos.Clear();
                    BillPhotoPath = null;
                    CapturedBillPhotoPath = null;
                    _isRetakingPhoto = false;
                    PhotoGridViewModel?.Clear();
                    ClearDisplayInfo();

                    // 更新预览状态
                    this.RaisePropertyChanged(nameof(HasBillPhoto));
                    this.RaisePropertyChanged(nameof(BillPhotoButtonText));
                    this.RaisePropertyChanged(nameof(ShouldShowPreview));
                    this.RaisePropertyChanged(nameof(CanPrintSolidWaste));
                }
            })
            .DisposeWith(_disposables);

        this.WhenAnyValue(x => x.CameraStatuses.Count)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(HasCameraStatuses)))
            .DisposeWith(_disposables);

        this.WhenAnyValue(x => x.SelectedListItem)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(DeliveryTypeTitleText)))
            .DisposeWith(_disposables);

        this.WhenAnyValue(x => x.IsShowingMainView)
            .Subscribe(async isShowingMainView =>
            {
                this.RaisePropertyChanged(nameof(IsShowingDetailView));

                // 当切换到 MainView 时，停止摄像头预览
                if (isShowingMainView)
                {
                    await StopUsbCameraPreviewAsync();
                }
                // 当从 MainView 切换到 DetailView 时，如果条件满足，启动预览
                else
                {
                    if (DocumentCameraEnabled && IsUsbCameraOnline && ShouldShowPreview)
                        await StartUsbCameraPreviewAsync();
                }
            })
            .DisposeWith(_disposables);

        this.WhenAnyValue(x => x.CurrentPage, x => x.TotalPages)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(PageInfoText)))
            .DisposeWith(_disposables);

        this.WhenAnyValue(x => x.IsReceiving)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(IsSending)))
            .DisposeWith(_disposables);

        // 监听BillPhotoPath变化，更新相关计算属性
        this.WhenAnyValue(x => x.BillPhotoPath)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(HasBillPhoto));
                this.RaisePropertyChanged(nameof(BillPhotoButtonText));
                this.RaisePropertyChanged(nameof(ShouldShowPreview));
            })
            .DisposeWith(_disposables);

        // 实时搜索响应（带防抖）
        this.WhenAnyValue(
                x => x.SearchStartDate,
                x => x.SearchEndDate,
                x => x.SearchPlateNumber)
            .Throttle(TimeSpan.FromMilliseconds(500)) // 防抖500ms
            .DistinctUntilChanged()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(async _ =>
            {
                CurrentPage = 1; // 重置到第一页
                await RefreshAsync();
            })
            .DisposeWith(_disposables);

        _ = InitializeOnFirstLoadAsync();
        StartTimeUpdateTimer();

        _truckScaleWeightService.WeightUpdates
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(weight =>
            {
                Logger?.LogDebug($"UI Weight Update: {weight}");
                CurrentWeight = weight;
            })
            .DisposeWith(_disposables);

        StartScaleStatusCheckTimer();
        _ = CheckCameraStatusOnceAsync();
        _ = LoadDeviceVisibilitySettingsAsync();
        StartPrinterStatusCheckTimer();
        _ = CheckLprOnlineStatusAsync();
        StartLprStatusCheckTimer();
        StartUsbCameraStatusCheckTimer();
        _ = StartAllDevicesAsync();
        SyncDeviceStatuses();

        // Initialize state from service
        if (_attendedWeighingService != null)
        {
            _currentWeighingStatus = _attendedWeighingService.GetCurrentStatus();
            MostFrequentPlateNumber = _attendedWeighingService.GetMostFrequentPlateNumber();
            IsReceiving = _attendedWeighingService.CurrentDeliveryType == DeliveryType.Receiving;
        }

        StartStatusChangedEventSubscription();
        StartPlateNumberChangedEventSubscription();
        StartWeighingRecordCreatedEventSubscription();
        StartDeliveryTypeChangedEventSubscription();
        StartMatchSucceededEventSubscription();
        StartSaveCompletedEventSubscription();
        StartUpdatePlateNumberEventSubscription();
        StartSettingsSavedEventSubscription();
        StartDetailOperationCompletedEventSubscription();
        StartDetailCloseRequestedEventSubscription();
        StartManualMatchSaveCompletedEventSubscription();

        this.WhenAnyValue(x => x.PrinterName)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(PrinterTooltip)))
            .DisposeWith(_disposables);

        // 监听 USB 摄像头在线状态变化、ShouldShowPreview变化和IsShowingMainView变化，自动启动/停止预览
        this.WhenAnyValue(
                x => x.DocumentCameraEnabled,
                x => x.IsUsbCameraOnline,
                x => x.ShouldShowPreview,
                x => x.IsShowingMainView)
            .DistinctUntilChanged()
            .Subscribe(async tuple =>
            {
                var (documentCameraEnabled, isOnline, shouldShow, isShowingMainView) = tuple;
                if (documentCameraEnabled && isOnline && shouldShow && !isShowingMainView)
                    // 摄像头上线且应该显示预览且不在 MainView，启动预览
                    await StartUsbCameraPreviewAsync();
                else
                    // 摄像头下线、不应显示预览或处于 MainView，停止预览
                    await StopUsbCameraPreviewAsync();
            })
            .DisposeWith(_disposables);

        // 尝试初始启动预览（如果摄像头已经在线且应该显示预览）
        _ = StartUsbCameraPreviewAsync();
    }

    public void Dispose()
    {
        _ = StopUsbCameraPreviewAsync();
        _notificationFadeOutTimer?.Stop();
        _notificationFadeOutTimer = null;
        _disposables.Dispose();
    }

    /// <summary>
    ///     Sync the DeviceStatuses collection from individual device status properties.
    ///     Called via ReactiveUI subscription whenever any device status changes.
    /// </summary>
    private void SyncDeviceStatuses()
    {
        var visibility = DeviceStatusCatalog.FromSettings(
            DocumentCameraEnabled,
            IsPrinterEnabled,
            IsSoundDeviceEnabled);

        var items = DeviceStatusCatalog.BuildItems(
            visibility,
            IsScaleOnline,
            IsCameraOnline,
            IsUsbCameraOnline,
            IsPrinterOnline,
            IsLprOnline,
            IsSoundDeviceOnline);

        DeviceStatuses.Clear();
        foreach (var item in items)
        {
            DeviceStatuses.Add(item);
        }
    }

    /// <summary>
    ///     页面首次加载时的初始化逻辑
    /// </summary>
    public async Task InitializeOnFirstLoadAsync()
    {
        try
        {
            var settings = await _settingsService.GetSettingsAsync();
            IsSolidWasteMode = settings.SystemSettings.DefaultWeighingMode == WeighingMode.SolidWaste;
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "读取称重模式失败");
        }

        await RefreshAsync();
        BackToMain();
        await SelectLatestCompletedItemAsync();
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
            Logger?.LogError(ex, "启动设备失败");
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

    private async Task CheckCameraStatusOnceAsync()
    {
        try
        {
            await CheckCameraOnlineStatusAsync();
        }
        catch
        {
            Dispatcher.UIThread.Post(() => { IsCameraOnline = false; });
        }
    }

    private async Task LoadDeviceVisibilitySettingsAsync()
    {
        try
        {
            var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
            var settings = await settingsService.GetSettingsAsync();

            DocumentCameraEnabled = settings.SystemSettings.DocumentCameraEnabled;
            IsPrinterEnabled = settings.SystemSettings.EnablePrinter;
            PrinterName = settings.SystemSettings.SelectedPrinterName ?? string.Empty;

            var soundEnabled = settings.SoundDeviceSettings.Enabled;
            if (soundEnabled && _statusPollingDisposable == null)
                InitializeSoundDeviceStatusPolling();
            else if (!soundEnabled)
            {
                _statusPollingDisposable?.Dispose();
                _statusPollingDisposable = null;
                _soundDeviceStatus.OnNext(-1);
            }

            this.RaisePropertyChanged(nameof(IsSoundDeviceEnabled));

            if (DocumentCameraEnabled)
                await CheckUsbCameraOnlineStatusAsync();
            else
                Dispatcher.UIThread.Post(() => { IsUsbCameraOnline = false; });

            if (IsPrinterEnabled)
                await CheckPrinterStatusOnceAsync();
            else
                Dispatcher.UIThread.Post(() => { IsPrinterOnline = false; });

            SyncDeviceStatuses();
        }
        catch (Exception ex)
        {
            Logger?.LogWarning(ex, "Failed to load device visibility settings");
            Dispatcher.UIThread.Post(() =>
            {
                DocumentCameraEnabled = false;
                IsPrinterEnabled = false;
                IsPrinterOnline = false;
                IsUsbCameraOnline = false;
            });
            SyncDeviceStatuses();
        }
    }

    private async Task CheckPrinterStatusOnceAsync()
    {
        try
        {
            var printingService = _serviceProvider.GetService<ITicketPrintingService>();
            if (printingService == null || string.IsNullOrWhiteSpace(PrinterName))
            {
                Dispatcher.UIThread.Post(() => { IsPrinterOnline = false; });
                return;
            }

            var installedPrinters = await Task.Run(() => printingService.ListInstalledPrinters());
            var isOnline =
                installedPrinters.Any(p => string.Equals(p, PrinterName, StringComparison.OrdinalIgnoreCase));

            Dispatcher.UIThread.Post(() => { IsPrinterOnline = isOnline; });
        }
        catch (Exception ex)
        {
            Logger?.LogWarning(ex, "Failed to check printer status");
            Dispatcher.UIThread.Post(() => { IsPrinterOnline = false; });
        }
    }

    private void StartPrinterStatusCheckTimer()
    {
        var printerStatusTimer = new Timer(_ =>
        {
            if (!IsPrinterEnabled) return;

            Task.Run(async () =>
            {
                try
                {
                    await CheckPrinterStatusOnceAsync();
                }
                catch
                {
                    Dispatcher.UIThread.Post(() => { IsPrinterOnline = false; });
                }
            });
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));

        _disposables.Add(printerStatusTimer);
    }

    private void StartUsbCameraStatusCheckTimer()
    {
        var usbCameraStatusTimer = new Timer(_ =>
        {
            if (!DocumentCameraEnabled) return;

            Task.Run(async () =>
            {
                try
                {
                    await CheckUsbCameraOnlineStatusAsync();
                }
                catch
                {
                    Dispatcher.UIThread.Post(() => { IsUsbCameraOnline = false; });
                }
            });
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));

        _disposables.Add(usbCameraStatusTimer);
    }

    private async Task CheckUsbCameraOnlineStatusAsync()
    {
        if (!DocumentCameraEnabled)
        {
            Dispatcher.UIThread.Post(() => { IsUsbCameraOnline = false; });
            return;
        }

        try
        {
            var usbCameraService = _serviceProvider.GetService<IUsbCameraService>();
            if (usbCameraService == null)
            {
                Dispatcher.UIThread.Post(() => { IsUsbCameraOnline = false; });
                return;
            }

            var isOnline = await usbCameraService.IsAvailableAsync();
            Dispatcher.UIThread.Post(() => { IsUsbCameraOnline = isOnline; });
        }
        catch (Exception ex)
        {
            Logger?.LogWarning(ex, "检查 USB 摄像头状态时发生错误");
            Dispatcher.UIThread.Post(() => { IsUsbCameraOnline = false; });
        }
    }

    private async Task CheckLprOnlineStatusAsync()
    {
        try
        {
            var settings = await _settingsService.GetSettingsAsync();
            var deviceType = settings.SystemSettings.LprDeviceType;
            var configs = settings.LicensePlateRecognitionConfigs;

            if (configs == null || configs.Count == 0)
            {
                Dispatcher.UIThread.Post(() => { IsLprOnline = false; });
                return;
            }

            var statuses = _lprDeviceOnlineStatusService.GetOnlineStatuses(deviceType, configs);
            var anyOnline = statuses.Any(s => s.IsOnline);

            Dispatcher.UIThread.Post(() => { IsLprOnline = anyOnline; });
        }
        catch (Exception ex)
        {
            Logger?.LogWarning(ex, "检查车牌识别设备状态时发生错误");
            Dispatcher.UIThread.Post(() => { IsLprOnline = false; });
        }
    }

    private void StartLprStatusCheckTimer()
    {
        var lprStatusTimer = new Timer(_ =>
        {
            Task.Run(async () =>
            {
                try
                {
                    await CheckLprOnlineStatusAsync();
                }
                catch
                {
                    Dispatcher.UIThread.Post(() => { IsLprOnline = false; });
                }
            });
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));

        _disposables.Add(lprStatusTimer);
    }

    /// <summary>
    ///     Initialize sound column device status polling
    /// </summary>
    private void InitializeSoundDeviceStatusPolling()
    {
        if (!IsSoundDeviceEnabled) return;

        _statusPollingDisposable?.Dispose();
        // Timer(0, 8s) = first poll immediately, then every 8 seconds (Interval would wait 8s for first)
        _statusPollingDisposable = Observable
            .Timer(TimeSpan.Zero, TimeSpan.FromSeconds(8))
            .SelectMany(_ => Observable.FromAsync(cancellationToken =>
                _soundDeviceService.IsOnlineAsync()))
            .Select(isOnline => isOnline ? 1 : 0) // Convert bool to status code
            .Retry(3) // Retry 3 times on failure
            .Catch(Observable.Return(-1)) // Return unknown status on exception
            .Subscribe(
                status =>
                {
                    _soundDeviceStatus.OnNext(status);
                    this.RaisePropertyChanged(nameof(IsSoundDeviceOnline));
                    this.RaisePropertyChanged(nameof(SoundDeviceStatusColor));
                    this.RaisePropertyChanged(nameof(SoundDeviceStatusText));
                    SyncDeviceStatuses();
                },
                ex => Logger?.LogError(ex, "Error in sound device status polling"));
    }

    private async Task StartUsbCameraPreviewAsync()
    {
        try
        {
            if (!DocumentCameraEnabled)
                return;

            // 如果处于 MainView，则不启动预览
            if (IsShowingMainView)
            {
                Logger?.LogDebug("处于 MainView，跳过预览启动");
                return;
            }

            // 如果不应显示预览（已存在BillPhoto且未在重新拍照模式），则不启动预览
            if (!ShouldShowPreview)
            {
                Logger?.LogDebug("已存在BillPhoto且未在重新拍照模式，跳过预览启动");
                return;
            }

            var usbCameraService = _serviceProvider.GetService<IUsbCameraService>();
            if (usbCameraService == null) return;

            // 如果预览已经在运行，跳过
            if (usbCameraService.IsPreviewing)
            {
                Logger?.LogDebug("USB 摄像头预览已在运行中，跳过启动");
                return;
            }

            var isAvailable = await usbCameraService.IsAvailableAsync();
            if (!isAvailable)
            {
                Logger?.LogDebug("USB 摄像头不可用，跳过预览启动");
                return;
            }

            await usbCameraService.StartPreviewAsync((imageBytes, width, height) =>
            {
                try
                {
                    // 帧计数器递增，只处理一半的帧（每两帧处理一帧）
                    _frameCounter++;
                    if (_frameCounter % 2 != 0) return; // 跳过奇数帧，只处理偶数帧

                    // 在 UI 线程上更新图像
                    Dispatcher.UIThread.Post(() =>
                    {
                        try
                        {
                            // 释放旧的 Bitmap
                            var oldBitmap = UsbCameraPreview;

                            // 将字节数组转换为 Bitmap
                            using var stream = new MemoryStream(imageBytes);
                            var bitmap = new Bitmap(stream);
                            UsbCameraPreview = bitmap;

                            // 释放旧的 Bitmap
                            oldBitmap?.Dispose();
                        }
                        catch (Exception ex)
                        {
                            Logger?.LogWarning(ex, "更新 USB 摄像头预览图像时发生错误");
                        }
                    });
                }
                catch (Exception ex)
                {
                    Logger?.LogWarning(ex, "处理 USB 摄像头帧数据时发生错误");
                }
            });

            Logger?.LogInformation("USB 摄像头预览已启动");
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "启动 USB 摄像头预览时发生错误");
        }
    }

    private async Task StopUsbCameraPreviewAsync()
    {
        try
        {
            var usbCameraService = _serviceProvider.GetService<IUsbCameraService>();
            if (usbCameraService == null) return;

            // 如果预览未在运行，跳过
            if (!usbCameraService.IsPreviewing)
            {
                Logger?.LogDebug("USB 摄像头预览未在运行，跳过停止");
                return;
            }

            await usbCameraService.StopPreviewAsync();
            Dispatcher.UIThread.Post(() =>
            {
                UsbCameraPreview?.Dispose();
                UsbCameraPreview = null;
            });
            Logger?.LogInformation("USB 摄像头预览已停止");
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "停止 USB 摄像头预览时发生错误");
        }
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
            var anyOnline = false;

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

                if (isOnline) anyOnline = true;
            }

            Dispatcher.UIThread.Post(() =>
            {
                IsCameraOnline = anyOnline;
                CameraStatuses.Clear();
                foreach (var status in cameraStatusList) CameraStatuses.Add(status);
                SyncCameraStatusDetails();
            });
        }
        catch
        {
            Dispatcher.UIThread.Post(() =>
            {
                IsCameraOnline = false;
                CameraStatuses.Clear();
                SyncCameraStatusDetails();
            });
        }
    }

    private void SyncCameraStatusDetails()
    {
        CameraStatusDetails.Clear();
        foreach (var status in CameraStatuses)
        {
            CameraStatusDetails.Add(new CameraStatusDetailItem(
                status.Name,
                status.Ip,
                status.Port,
                status.IsOnline));
        }
    }

    private void StartTimeUpdateTimer()
    {
        var timeTimer = new Timer(_ => CurrentTime = DateTime.Now, null,
            TimeSpan.Zero, TimeSpan.FromSeconds(1));
        _disposables.Add(timeTimer);
    }


    /// <summary>
    ///     订阅状态变化事件（通过 ILocalEventBus）
    /// </summary>
    private void StartStatusChangedEventSubscription()
    {
        _localEventBus.Subscribe<StatusChangedEventData>(eventData =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    _currentWeighingStatus = eventData.Status;
                    this.RaisePropertyChanged(nameof(CurrentWeighingStatusText));
                    this.RaisePropertyChanged(nameof(IsWeighingActive));
                });
                return Task.CompletedTask;
            })
            .DisposeWith(_disposables);
    }

    /// <summary>
    ///     订阅车牌号变化事件（通过 ILocalEventBus）
    /// </summary>
    private void StartPlateNumberChangedEventSubscription()
    {
        _localEventBus.Subscribe<PlateNumberChangedEventData>(eventData =>
            {
                Dispatcher.UIThread.Post(() => { MostFrequentPlateNumber = eventData.PlateNumber; });
                return Task.CompletedTask;
            })
            .DisposeWith(_disposables);
    }

    /// <summary>
    ///     订阅称重记录创建事件（通过 ILocalEventBus）
    /// </summary>
    private void StartWeighingRecordCreatedEventSubscription()
    {
        _localEventBus.Subscribe<WeighingRecordCreatedEventData>(async eventData =>
            {
                Logger?.LogInformation("接收到新称重记录创建事件, ID: {WeighingRecordId}", eventData.WeighingRecordId);
                await RefreshAsync();
            })
            .DisposeWith(_disposables);
    }

    /// <summary>
    ///     订阅收发料类型变化事件（通过 ILocalEventBus）
    /// </summary>
    private void StartDeliveryTypeChangedEventSubscription()
    {
        _localEventBus.Subscribe<DeliveryTypeChangedEventData>(eventData =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    IsReceiving = eventData.DeliveryType == DeliveryType.Receiving;
                    ShowDeliveryTypeChangedNotification(eventData.DeliveryType);
                });
                return Task.CompletedTask;
            })
            .DisposeWith(_disposables);
    }

    private void ShowDeliveryTypeChangedNotification(DeliveryType deliveryType)
    {
        try
        {
            var modeText = deliveryType == DeliveryType.Receiving ? "收料" : "发料";
            var modeColor = deliveryType == DeliveryType.Receiving ? Brushes.Green : Brushes.Red;
            
            // Clear existing inlines and add new formatted text
            var inlines = _notificationTextBlockHolder.Inlines;
            inlines.Clear();
            inlines.Add(new Run("称重模式已切换到") { Foreground = Brushes.White });
            inlines.Add(new Run(modeText) { Foreground = modeColor, FontWeight = FontWeight.Bold, FontSize = 16 });
            
            // Notify that Inlines collection has changed
            this.RaisePropertyChanged(nameof(DeliveryTypeNotificationInlines));
            
            // Set fade in
            DeliveryTypeNotificationOpacity = 1.0;

            // Stop any existing timer
            _notificationFadeOutTimer?.Stop();

            // Create and start fade out timer (2 seconds display, then fade out)
            _notificationFadeOutTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _notificationFadeOutTimer.Tick += (sender, e) =>
            {
                _notificationFadeOutTimer.Stop();
                DeliveryTypeNotificationOpacity = 0.0;
            };
            _notificationFadeOutTimer.Start();
        }
        catch (Exception ex)
        {
            Logger?.LogDebug(ex, "Failed to show delivery type change notification");
        }
    }

    /// <summary>
    ///     订阅匹配成功事件（通过 ILocalEventBus）
    /// </summary>
    private void StartMatchSucceededEventSubscription()
    {
        _localEventBus.Subscribe<MatchSucceededEventData>(async eventData =>
            {
                Logger?.LogInformation(
                    "AttendedWeighingViewModel: Received MatchSucceededEventData for WaybillId {WaybillId}, WeighingRecordId {RecordId}",
                    eventData.WaybillId, eventData.WeighingRecordId);

                try
                {
                    // 刷新列表
                    await RefreshAsync();

                    // 查找匹配成功的 Waybill 列表项
                    var matchedItem = ListItems
                        .FirstOrDefault(item =>
                            item.ItemType == WeighingListItemType.Waybill &&
                            item.Id == eventData.WaybillId);

                    if (matchedItem != null)
                    {
                        // 直接设置 SelectedListItem，确保使用刷新后的对象引用
                        // 这样 EqualityToColorConverter 才能正确比较对象引用
                        SelectedListItem = matchedItem;

                        // 根据项类型执行相应的选择逻辑
                        if (matchedItem is
                            { ItemType: WeighingListItemType.Waybill, OrderType: OrderTypeEnum.Completed })
                            SelectCompletedWaybill(matchedItem);
                        else
                            _ = OpenDetail(matchedItem);

                        Logger?.LogInformation(
                            "AttendedWeighingViewModel: Selected matched Waybill {WaybillId}",
                            eventData.WaybillId);
                    }
                    else
                    {
                        Logger?.LogWarning(
                            "AttendedWeighingViewModel: Matched Waybill {WaybillId} not found in current list",
                            eventData.WaybillId);
                    }
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex,
                        "AttendedWeighingViewModel: Error while handling MatchSucceededMessage for WaybillId {WaybillId}",
                        eventData.WaybillId);
                }
            })
            .DisposeWith(_disposables);
    }

    /// <summary>
    ///     订阅保存完成事件（通过 ILocalEventBus）
    /// </summary>
    private void StartSaveCompletedEventSubscription()
    {
        _localEventBus.Subscribe<SaveCompletedEventData>(async eventData =>
            {
                Logger?.LogInformation(
                    "AttendedWeighingViewModel: Received SaveCompletedEventData for ItemId {ItemId}, ItemType {ItemType}",
                    eventData.ItemId, eventData.ItemType);

                try
                {
                    // 刷新列表
                    await RefreshAsync();

                    // 查找保存的列表项
                    var savedItem = ListItems
                        .FirstOrDefault(item =>
                            item.ItemType == eventData.ItemType &&
                            item.Id == eventData.ItemId);

                    if (savedItem != null)
                    {
                        // 直接设置 SelectedListItem，确保使用刷新后的对象引用
                        // 这样 EqualityToColorConverter 才能正确比较对象引用
                        SelectedListItem = savedItem;

                        // 根据项类型执行相应的选择逻辑
                        if (savedItem is { ItemType: WeighingListItemType.Waybill, OrderType: OrderTypeEnum.Completed })
                            SelectCompletedWaybill(savedItem);
                        else
                            _ = OpenDetail(savedItem);

                        Logger?.LogInformation(
                            "AttendedWeighingViewModel: Selected saved item {ItemId} of type {ItemType}",
                            eventData.ItemId, eventData.ItemType);
                    }
                    else
                    {
                        Logger?.LogWarning(
                            "AttendedWeighingViewModel: Saved item {ItemId} of type {ItemType} not found in current list",
                            eventData.ItemId, eventData.ItemType);
                    }
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex,
                        "AttendedWeighingViewModel: Error while handling SaveCompletedEventData for ItemId {ItemId}",
                        eventData.ItemId);
                }
            })
            .DisposeWith(_disposables);
    }

    /// <summary>
    ///     订阅更新车牌号事件（通过 ILocalEventBus）
    /// </summary>
    private void StartUpdatePlateNumberEventSubscription()
    {
        _localEventBus.Subscribe<UpdatePlateNumberEventData>(async eventData =>
            {
                Logger?.LogInformation(
                    "AttendedWeighingViewModel: Received UpdatePlateNumberEventData for WeighingRecordId {RecordId}, PlateNumber {PlateNumber}",
                    eventData.WeighingRecordId, eventData.PlateNumber);

                try
                {
                    // 刷新列表以更新车牌号
                    await RefreshAsync();

                    Logger?.LogInformation(
                        "AttendedWeighingViewModel: Refreshed list after plate number update for WeighingRecordId {RecordId}",
                        eventData.WeighingRecordId);
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex,
                        "AttendedWeighingViewModel: Error while handling UpdatePlateNumberEventData for WeighingRecordId {RecordId}",
                        eventData.WeighingRecordId);
                }
            })
            .DisposeWith(_disposables);
    }

    /// <summary>
    ///     订阅设置已保存事件（通过 ILocalEventBus）
    /// </summary>
    private void StartSettingsSavedEventSubscription()
    {
        _localEventBus.Subscribe<SettingsSavedEventData>(async _ =>
            {
                Logger?.LogInformation(
                    "AttendedWeighingViewModel: Received SettingsSavedEventData, checking camera status");

                try
                {
                    await CheckCameraStatusOnceAsync();
                    await LoadDeviceVisibilitySettingsAsync();
                    await CheckLprOnlineStatusAsync();
                    Logger?.LogInformation(
                        "AttendedWeighingViewModel: Device status bar refreshed after settings save");
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex,
                        "AttendedWeighingViewModel: Error while checking camera status after settings save");
                }
            })
            .DisposeWith(_disposables);
    }

    /// <summary>
    ///     Subscribe to detail operation completed messages (replacing direct event subscriptions).
    ///     Dispatches to the appropriate handler based on OperationType.
    /// </summary>
    private void StartDetailOperationCompletedEventSubscription()
    {
        _localEventBus.Subscribe<DetailOperationCompletedEventData>(async message =>
            {
                Logger?.LogInformation(
                    "AttendedWeighingViewModel: Received DetailOperationCompletedEventData, OperationType={OperationType}, ItemId={ItemId}",
                    message.OperationType, message.ItemId);

                try
                {
                    switch (message.OperationType)
                    {
                        case DetailOperationType.Save:
                            await OnDetailSaveCompleted(message);
                            break;
                        case DetailOperationType.Abolish:
                            await OnDetailAbolishCompleted(message);
                            break;
                        case DetailOperationType.Match:
                            await OnDetailMatchCompleted(message);
                            break;
                        case DetailOperationType.Complete:
                            await OnDetailCompleteCompleted(message);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex,
                        "AttendedWeighingViewModel: Error handling DetailOperationCompletedEventData for ItemId {ItemId}",
                        message.ItemId);
                }
            })
            .DisposeWith(_disposables);
    }

    /// <summary>
    ///     Subscribe to detail close requested messages (replacing CloseRequested event).
    /// </summary>
    private void StartDetailCloseRequestedEventSubscription()
    {
        _localEventBus.Subscribe<DetailCloseRequestedEventData>(async _ =>
            {
                Logger?.LogInformation("AttendedWeighingViewModel: Received DetailCloseRequestedEventData");

                try
                {
                    await OnDetailCloseRequested();
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "AttendedWeighingViewModel: Error handling DetailCloseRequestedEventData");
                }
            })
            .DisposeWith(_disposables);
    }

    /// <summary>
    ///     Subscribe to manual match save completed messages (replacing ManualMatchSaveCompleted event).
    /// </summary>
    private void StartManualMatchSaveCompletedEventSubscription()
    {
        _localEventBus.Subscribe<ManualMatchSaveCompletedEventData>(async message =>
            {
                Logger?.LogInformation(
                    "AttendedWeighingViewModel: Received ManualMatchSaveCompletedEventData, WaybillId={WaybillId}",
                    message.WaybillId);

                try
                {
                    await OnDetailManualMatchSaveCompleted(message);
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex,
                        "AttendedWeighingViewModel: Error handling ManualMatchSaveCompletedEventData for WaybillId {WaybillId}",
                        message.WaybillId);
                }
            })
            .DisposeWith(_disposables);
    }

    private static string GetStatusText(AttendedWeighingStatus status) =>
        AttendedWeighingStatusDisplay.GetStatusText(status);

    /// <summary>
    ///     从列表项加载照片（统一接口）
    /// </summary>
    private async Task LoadListItemPhotos(WeighingListItemDto item)
    {
        try
        {
            if (PhotoGridViewModel != null) await PhotoGridViewModel.LoadFromListItemAsync(item);

            var attachmentService = _serviceProvider.GetService<IAttachmentService>();
            if (attachmentService != null)
            {
                var attachmentFiles = await attachmentService.GetAttachmentsByListItemAsync(item);

                VehiclePhotos.Clear();
                BillPhotoPath = null;

                foreach (var file in attachmentFiles)
                    if (!string.IsNullOrEmpty(file.LocalPath))
                    {
                        if (file.AttachType == AttachType.EntryPhoto ||
                            file.AttachType == AttachType.ExitPhoto)
                            VehiclePhotos.Add(file.LocalPath);
                        else if (file.AttachType == AttachType.TicketPhoto) BillPhotoPath = file.LocalPath;
                    }

                // 重置重新拍照状态和临时文件路径
                _isRetakingPhoto = false;
                CapturedBillPhotoPath = null;
            }
        }
        catch
        {
            // If service is not available, photos will remain empty
        }
    }

    #region Properties

    [Reactive] private ObservableCollection<WeighingListItemDto> _listItems = new();

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

    [Reactive] private string? _offsetBlockTitle;

    [Reactive] private string? _offsetBlockValue;

    [Reactive] private string? _joinWeightInfo;

    [Reactive] private string? _outWeightInfo;

    [Reactive] private bool _isScaleOnline;

    [Reactive] private bool _isCameraOnline;

    [Reactive] private bool _isUsbCameraOnline;

    [Reactive] private bool _documentCameraEnabled;

    [Reactive] private bool _isPrinterEnabled;

    [Reactive] private bool _isPrinterOnline;

    [Reactive] private bool _isSolidWasteMode;

    [Reactive] private bool _isLprOnline;

    [Reactive] private string _printerName = string.Empty;

    [Reactive] private Bitmap? _usbCameraPreview;

    [Reactive] private ObservableCollection<CameraStatusViewModel> _cameraStatuses = new();

    /// <summary>
    ///     Per-camera rows for DeviceStatusBar hover popup (MaterialClient.UI).
    /// </summary>
    [Reactive]
    public ObservableCollection<CameraStatusDetailItem> CameraStatusDetails { get; set; } = [];

    /// <summary>
    ///     Aggregated device status items for the shared DeviceStatusBar control.
    ///     Updated whenever individual device status properties change.
    /// </summary>
    [Reactive]
    private ObservableCollection<DeviceStatusItem> _deviceStatuses = new(
        DeviceStatusCatalog.BuildItems(DeviceStatusBarOptions.CoreOnly, false, false, false, false, false));

    public bool HasCameraStatuses => CameraStatuses.Count > 0;

    [Reactive] private string? _mostFrequentPlateNumber;

    [Reactive] private bool _isShowingMainView = true;

    public bool IsShowingDetailView => !IsShowingMainView;

    public InlineCollection DeliveryTypeNotificationInlines => _notificationTextBlockHolder.Inlines;

    [Reactive] private double _deliveryTypeNotificationOpacity;

    [Reactive] private AttendedWeighingDetailViewModelBase? _detailViewModel;

    [Reactive] private int _currentPage = 1;

    [Reactive] private int _pageSize = 6;

    [Reactive] private int _totalCount;

    [Reactive] private int _totalPages;

    [Reactive] private DateTime? _searchStartDate;

    [Reactive] private DateTime? _searchEndDate;

    [Reactive] private string? _searchPlateNumber;

    private bool _isRetakingPhoto; // 是否在重新拍照模式
    private int _frameCounter; // 帧计数器，用于只处理一半的帧

    public string CurrentWeighingStatusText => GetStatusText(_currentWeighingStatus);
    public bool IsWeighingActive => _currentWeighingStatus != AttendedWeighingStatus.OffScale;

    public bool IsSyncing
    {
        get => _isSyncing;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isSyncing, value);
        }
    }

    public bool IsCompletedWaybillSelected => SelectedListItem is
        { ItemType: WeighingListItemType.Waybill, OrderType: OrderTypeEnum.Completed };

    public bool CanPrintSolidWaste => SelectedListItem is
    {
        ItemType: WeighingListItemType.Waybill, OrderType: OrderTypeEnum.Completed,
        WeighingMode: WeighingMode.SolidWaste
    };

    public bool CanEditSolidWaste => SelectedListItem is
    {
        ItemType: WeighingListItemType.Waybill,
        WeighingMode: WeighingMode.SolidWaste
    };

    public string DeliveryTypeTitleText =>
        SelectedListItem?.DeliveryType == DeliveryType.Receiving ? "收料信息" : "发料信息";

    public string PageInfoText => $"第 {CurrentPage} / {TotalPages} 页";
    public bool IsSending => !IsReceiving;

    /// <summary>
    ///     检查当前SelectedListItem是否已有TicketPhoto类型的附件
    /// </summary>
    public bool HasBillPhoto => !string.IsNullOrEmpty(BillPhotoPath);

    /// <summary>
    ///     拍照按钮文本
    /// </summary>
    public string BillPhotoButtonText => HasBillPhoto && !_isRetakingPhoto ? "重新拍照" : "拍照";

    /// <summary>
    ///     是否应该显示预览（当存在BillPhoto且未点击重新拍照时不显示预览）
    /// </summary>
    public bool ShouldShowPreview => !HasBillPhoto || _isRetakingPhoto;

    public string PrinterTooltip => string.IsNullOrWhiteSpace(PrinterName) ? "未选择打印机" : PrinterName;

    /// <summary>
    ///     Whether sound column device is online
    /// </summary>
    public bool IsSoundDeviceOnline => _soundDeviceStatus.Value == 1 || _soundDeviceStatus.Value == 2;

    /// <summary>
    ///     Whether sound column device is enabled
    /// </summary>
    public bool IsSoundDeviceEnabled
    {
        get
        {
            try
            {
                var settings = _settingsService.GetSettingsAsync().GetAwaiter().GetResult();
                return settings.SoundDeviceSettings.Enabled;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    ///     Sound column device status color
    /// </summary>
    public Color SoundDeviceStatusColor => _soundDeviceStatus.Value switch
    {
        1 => Color.Parse("#10B981"), // Online - Green
        2 => Color.Parse("#F59E0B"), // In Task - Yellow
        3 => Color.Parse("#EF4444"), // Power Off - Red
        _ => Color.Parse("#9CA3AF")  // Offline/Unknown - Gray
    };

    /// <summary>
    ///     Sound column device status text
    /// </summary>
    public string SoundDeviceStatusText => _soundDeviceStatus.Value switch
    {
        0 => "离线",
        1 => "在线",
        2 => "任务中",
        3 => "断电",
        _ => "未知"
    };

    /// <summary>
    ///     获取临时保存的拍照文件路径（供DetailViewModel使用）
    /// </summary>
    public string? CapturedBillPhotoPath { get; private set; }

    /// <summary>
    ///     清空临时保存的拍照文件路径（供DetailViewModel保存后调用）
    /// </summary>
    public void ClearCapturedBillPhotoPath()
    {
        CapturedBillPhotoPath = null;
    }

    #endregion

    #region Command Implementations

    [ReactiveCommand]
    private async Task RefreshAsync()
    {
        try
        {
            bool? isCompleted = null;
            if (IsShowUnmatched)
                isCompleted = false;
            else if (IsShowCompleted) isCompleted = true;

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
            foreach (var item in pagedItems)
            {
                ListItems.Add(item);
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
    private async Task ShowAllRecords()
    {
        await SetDisplayModeAsync(0);
    }

    [ReactiveCommand]
    private async Task ShowUnmatched()
    {
        await SetDisplayModeAsync(1);
    }

    [ReactiveCommand]
    private async Task ShowCompleted()
    {
        await SetDisplayModeAsync(2);
    }

    [ReactiveCommand]
    private void SelectListItem(WeighingListItemDto? item)
    {
        if (item == null) return;

        SelectedListItem = item;

        if (item is { ItemType: WeighingListItemType.Waybill, OrderType: OrderTypeEnum.Completed })
            SelectCompletedWaybill(item);
        else
            _ = OpenDetail(item);
    }

    private void SelectCompletedWaybill(WeighingListItemDto _)
    {
        // 直接使用 DTO 中的预计算字段，无需再次查询数据库
        // SelectedListItem 的变化会自动触发 UpdateDisplayInfoFromListItem
        IsShowingMainView = true;
    }

    /// <summary>
    ///     从列表项更新显示信息（使用预计算字段）
    /// </summary>
    private void UpdateDisplayInfoFromListItem(WeighingListItemDto item)
    {
        // 使用预计算的供应商名称和物料信息
        MaterialInfo = item.MaterialInfo;
        OffsetInfo = item.OffsetInfo;

        // Block 6：固废模式显示「净重」+ OrderGoodsWeight 吨，标准模式显示「偏差」+ OffsetInfo
        if (item.WeighingMode == WeighingMode.SolidWaste)
        {
            OffsetBlockTitle = "净重";
            OffsetBlockValue = item.OrderGoodsWeight.HasValue ? $"{item.OrderGoodsWeight.Value:F2} 吨" : "--";
        }
        else
        {
            OffsetBlockTitle = "偏差";
            OffsetBlockValue = item.OffsetInfo;
        }

        // 使用预计算的进出场重量
        if (item.JoinWeight.HasValue)
            JoinWeightInfo = $"{item.JoinWeight.Value:F2} 吨 {item.JoinTime:HH:mm:ss}";
        else
            JoinWeightInfo = null;

        if (item.OutWeight.HasValue && item.OutTime.HasValue)
            OutWeightInfo = $"{item.OutWeight.Value:F2} 吨 {item.OutTime.Value:HH:mm:ss}";
        else
            OutWeightInfo = null;
    }

    /// <summary>
    ///     清空显示信息
    /// </summary>
    private void ClearDisplayInfo()
    {
        MaterialInfo = null;
        OffsetInfo = null;
        OffsetBlockTitle = null;
        OffsetBlockValue = null;
        JoinWeightInfo = null;
        OutWeightInfo = null;
    }

    /// <summary>
    ///     从数据库重新加载列表项
    /// </summary>
    private async Task<WeighingListItemDto?> ReloadItemFromDatabaseAsync(long itemId, WeighingListItemType itemType)
    {
        try
        {
            var item = await _weighingMatchingService.GetListItemByIdAsync(itemId, itemType);
            if (item != null)
            {
                Logger?.LogInformation("ReloadItemFromDatabaseAsync: Successfully reloaded item {ItemId}", itemId);
            }
            else
            {
                Logger?.LogWarning("ReloadItemFromDatabaseAsync: Item {ItemId} not found in database", itemId);
            }

            return item;
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "ReloadItemFromDatabaseAsync: Failed to reload item {ItemId}", itemId);
            return null;
        }
    }

    [ReactiveCommand]
    private async Task OpenDetail(WeighingListItemDto? item)
    {
        if (item == null) return;

        try
        {
            
            var reloadedItem = await ReloadItemFromDatabaseAsync(item.Id, item.ItemType);
            if (reloadedItem == null)
            {
                Logger?.LogWarning("OpenDetailAsync: Failed to reload item from database, using cached item");
            }
            else
            {
                item = reloadedItem;
            }
            
            
            DetailViewModel = CreateDetailViewModel(item.WeighingMode);
            DetailViewModel.InitializeData(item, CapturedBillPhotoPath);

            IsShowingMainView = false;
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "打开详情视图失败");
        }
    }

    /// <summary>
    ///     打开详情视图（支持从数据库重新加载数据）
    /// </summary>
    /// <param name="item">列表项</param>
    /// <param name="reloadFromDb">是否从数据库重新加载</param>
    private async Task OpenDetailAsync(WeighingListItemDto? item, bool reloadFromDb = false)
    {
        if (item == null) return;

        try
        {
            // 如果需要从数据库重新加载
            if (reloadFromDb)
            {
                var reloadedItem = await ReloadItemFromDatabaseAsync(item.Id, item.ItemType);
                if (reloadedItem == null)
                {
                    Logger?.LogWarning("OpenDetailAsync: Failed to reload item from database, using cached item");
                }
                else
                {
                    item = reloadedItem;
                }
            }

            DetailViewModel = CreateDetailViewModel(item.WeighingMode);
            DetailViewModel.InitializeData(item, CapturedBillPhotoPath);

            IsShowingMainView = false;
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "打开详情视图失败");
        }
    }

    private AttendedWeighingDetailViewModelBase CreateDetailViewModel(WeighingMode weighingMode)
    {
        return weighingMode == WeighingMode.SolidWaste
            ? _serviceProvider.GetRequiredService<SolidWasteWeighingDetailViewModel>()
            : _serviceProvider.GetRequiredService<StandardWeighingDetailViewModel>();
    }

    [ReactiveCommand]
    private void BackToMain()
    {
        SelectedListItem = null;
        IsShowingMainView = true;
        DetailViewModel = null;
    }

    private async Task OnDetailSaveCompleted(DetailOperationCompletedEventData msg)
    {
        await NavigateToItemAsync(msg);
    }

    private async Task OnDetailAbolishCompleted(DetailOperationCompletedEventData msg)
    {
        // Abolish操作删除了item，所以导航到下一个未匹配项
        await RefreshAsync();
        await SelectUnmatchedNextItemAsync();
    }

    private async Task OnDetailMatchCompleted(DetailOperationCompletedEventData msg)
    {
        await NavigateToItemAsync(msg);
    }

    private async Task OnDetailCompleteCompleted(DetailOperationCompletedEventData msg)
    {
        if (IsSolidWasteMode)
        {
            // SolidWaste 模式：沿用现有导航逻辑
            await NavigateToItemAsync(msg);
        }
        else
        {
            // Standard 模式：按优先级选择下一个未完成条目
            await SelectNextUnfinishedItemAsync();
        }
    }

    private async Task OnDetailManualMatchSaveCompleted(ManualMatchSaveCompletedEventData msg)
    {
        // Navigate to the saved waybill using DetailOperationCompletedEventData-like navigation
        if (msg.WaybillId.HasValue)
        {
            await NavigateToItemAsync(new DetailOperationCompletedEventData(
                itemId: msg.WaybillId.Value,
                itemType: WeighingListItemType.Waybill,
                orderType: OrderTypeEnum.FirstWeight,
                isCompleted: false,
                operationType: DetailOperationType.Match));
        }
    }

    private async Task OnDetailCloseRequested()
    {
        await RefreshAsync();
        //BackToMain();
        await SelectUnmatchedNextItemAsync();
    }

    /// <summary>
    ///     统一的导航逻辑：根据操作上下文导航到目标项
    /// </summary>
    private async Task NavigateToItemAsync(DetailOperationCompletedEventData args)
    {
        try
        {
            Logger?.LogInformation(
                "NavigateToItemAsync: ItemId={ItemId}, ItemType={ItemType}, OrderType={OrderType}, OperationType={OperationType}",
                args.ItemId, args.ItemType, args.OrderType, args.OperationType);

            // 1. 判断是否需要切换tab（使用args判断，无需先刷新）
            var needSwitchTab = ShouldSwitchTab(args);
            if (needSwitchTab)
            {
                SwitchToAppropriateTab(args);
            }

            // 2. 刷新数据（现在已经在正确的tab上）
            await RefreshAsync();

            // 3. 跨页查找目标项
            var targetItem = await FindItemAcrossPagesAsync(args.ItemId, args.ItemType);

            if (targetItem != null)
            {
                // 4. 选择目标项
                SelectedListItem = targetItem;

                // 5. 选择正确的视图
                SelectViewForItem(targetItem);

                Logger?.LogInformation(
                    "NavigateToItemAsync: Successfully navigated to item {ItemId} on page {CurrentPage}",
                    args.ItemId, CurrentPage);
            }
            else
            {
                Logger?.LogWarning(
                    "NavigateToItemAsync: Target item {ItemId} not found after searching across pages",
                    args.ItemId);

                // 如果未找到目标项，根据操作类型选择备用行为
                if (args.OperationType == DetailOperationType.Complete)
                {
                    // Complete操作：尝试选择第一个已完成项
                    await SelectLatestCompletedItemAsync();
                }
                else
                {
                    // 其他操作：选择下一个未匹配项
                    await SelectUnmatchedNextItemAsync();
                }
            }
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "NavigateToItemAsync failed for ItemId={ItemId}", args.ItemId);
        }
    }

    /// <summary>
    ///     判断是否需要切换tab
    /// </summary>
    private bool ShouldSwitchTab(DetailOperationCompletedEventData args)
    {
        // 如果显示全部记录，永不切换tab
        if (IsShowAllRecords)
        {
            Logger?.LogDebug("ShouldSwitchTab: IsShowAllRecords=true, no tab switch needed");
            return false;
        }

        // 判断目标项是否已完成
        bool itemIsCompleted = args.IsCompleted;

        // 判断当前tab是否能显示目标项
        bool currentTabCanShowItem =
            (IsShowCompleted && itemIsCompleted) ||
            (IsShowUnmatched && !itemIsCompleted);

        bool shouldSwitch = !currentTabCanShowItem;

        Logger?.LogDebug(
            "ShouldSwitchTab: itemIsCompleted={ItemIsCompleted}, currentTab={CurrentTab}, shouldSwitch={ShouldSwitch}",
            itemIsCompleted,
            IsShowCompleted ? "Completed" : (IsShowUnmatched ? "Unmatched" : "All"),
            shouldSwitch);

        return shouldSwitch;
    }

    /// <summary>
    ///     切换到合适的tab以显示目标项
    /// </summary>
    private void SwitchToAppropriateTab(DetailOperationCompletedEventData args)
    {
        if (args.IsCompleted)
        {
            // 切换到已完成tab
            IsShowCompleted = true;
            IsShowUnmatched = false;
            IsShowAllRecords = false;
            Logger?.LogInformation("Switched to Completed tab");
        }
        else
        {
            // 切换到未完成tab
            IsShowUnmatched = true;
            IsShowCompleted = false;
            IsShowAllRecords = false;
            Logger?.LogInformation("Switched to Unmatched tab");
        }
    }

    /// <summary>
    ///     跨页查找目标项（限制最多搜索10页）
    /// </summary>
    private async Task<WeighingListItemDto?> FindItemAcrossPagesAsync(long itemId, WeighingListItemType itemType)
    {
        const int maxPagesToSearch = 10;
        var startPage = CurrentPage;

        // 首先在当前页查找（快速路径）
        var item = ListItems.FirstOrDefault(x => x.Id == itemId && x.ItemType == itemType);
        if (item != null)
        {
            Logger?.LogDebug("FindItemAcrossPagesAsync: Found item on current page {CurrentPage}", CurrentPage);
            return item;
        }

        Logger?.LogInformation(
            "FindItemAcrossPagesAsync: Item not found on current page, searching across pages (max {MaxPages})",
            maxPagesToSearch);

        // 从第1页开始搜索
        for (int page = 1; page <= Math.Min(TotalPages, maxPagesToSearch); page++)
        {
            if (page == CurrentPage)
            {
                // 已经搜索过当前页，跳过
                continue;
            }

            CurrentPage = page;
            await RefreshAsync();

            item = ListItems.FirstOrDefault(x => x.Id == itemId && x.ItemType == itemType);
            if (item != null)
            {
                Logger?.LogInformation(
                    "FindItemAcrossPagesAsync: Found item on page {Page}",
                    page);
                return item;
            }
        }

        // 未找到，恢复原页码
        CurrentPage = startPage;
        await RefreshAsync();

        Logger?.LogWarning(
            "FindItemAcrossPagesAsync: Item {ItemId} not found after searching {PagesSearched} pages",
            itemId, Math.Min(TotalPages, maxPagesToSearch));

        return null;
    }

    /// <summary>
    ///     根据项目状态选择合适的视图
    /// </summary>
    private void SelectViewForItem(WeighingListItemDto item)
    {
        // 规则：Waybill + Completed → MainView（只读摘要）
        // 其他 → DetailView（可编辑表单）
        if (item.ItemType == WeighingListItemType.Waybill && item.OrderType == OrderTypeEnum.Completed)
        {
            IsShowingMainView = true;
            Logger?.LogDebug("SelectViewForItem: Showing MainView for completed waybill");
        }
        else
        {
            // 打开DetailView
            _ = OpenDetail(item);
            Logger?.LogDebug("SelectViewForItem: Showing DetailView for editable item");
        }
    }

    /// <summary>
    ///     选择已完成的第一个数据
    /// </summary>
    private async Task SelectLatestCompletedItemAsync(long? id = null, OrderTypeEnum? orderType = null)
    {
        try
        {
            WeighingListItemDto? targetItem = null;

            // Case 1: Search for specific item by Id and OrderType
            if (id.HasValue && orderType.HasValue)
            {
                // Search in current list
                targetItem = ListItems.FirstOrDefault(item =>
                    item.Id == id.Value && item.OrderType == orderType.Value);

                if (targetItem == null)
                {
                    // Switch to completed mode and refresh
                    IsShowCompleted = true;
                    IsShowAllRecords = false;
                    IsShowUnmatched = false;
                    CurrentPage = 1;
                    await RefreshAsync();

                    // Search again after refresh
                    targetItem = ListItems.FirstOrDefault(item =>
                        item.Id == id.Value && item.OrderType == orderType.Value);

                    if (targetItem == null)
                    {
                        IsShowCompleted = false;
                        IsShowAllRecords = false;
                        IsShowUnmatched = true;
                        CurrentPage = 1;
                        await RefreshAsync();
                        targetItem = ListItems.FirstOrDefault(item =>
                            item.Id == id.Value && item.OrderType == orderType.Value);
                    }
                }
            }
            // Case 2: Use existing "select latest completed" logic
            else
            {
                // 从当前列表中查找第一个完成数据
                targetItem = ListItems.FirstOrDefault(item =>
                    item.OrderType == OrderTypeEnum.Completed);

                if (targetItem == null)
                {
                    // 如果当前页没有完成数据，切换到显示完成数据模式并刷新
                    IsShowCompleted = true;
                    IsShowAllRecords = false;
                    IsShowUnmatched = false;
                    CurrentPage = 1;
                    await RefreshAsync();

                    // 刷新后选择第一条（应该就是已完成的第一个）
                    targetItem = ListItems.FirstOrDefault();
                }
            }

            // Select and open the target item
            if (targetItem != null)
            {
                SelectedListItem = targetItem;

                // 根据项类型执行相应的选择逻辑，确保显示正确的视图
                if (targetItem is { ItemType: WeighingListItemType.Waybill, OrderType: OrderTypeEnum.Completed })
                    SelectCompletedWaybill(targetItem);
                else
                    _ = OpenDetail(targetItem);
            }
        }
        catch
        {
            // 如果出错，忽略错误，不影响主流程
        }
    }

    /// <summary>
    ///     Standard 模式下完成操作后的导航逻辑：按优先级选择下一个未完成条目。
    ///     优先级：未完成 Waybill → 未完成 WeighingRecord → 兜底已完成条目。
    /// </summary>
    private async Task SelectNextUnfinishedItemAsync()
    {
        try
        {
            await RefreshAsync();

            // 优先级 1：未完成 Waybill
            var unfinishedWaybill = ListItems.FirstOrDefault(item =>
                item.ItemType == WeighingListItemType.Waybill &&
                item.OrderType != OrderTypeEnum.Completed);

            if (unfinishedWaybill != null)
            {
                // 按需切换标签页（若当前标签页无法显示未完成项）
                if (!IsShowAllRecords && (IsShowCompleted || !IsShowUnmatched))
                {
                    IsShowUnmatched = true;
                    IsShowCompleted = false;
                    IsShowAllRecords = false;
                    CurrentPage = 1;
                    await RefreshAsync();

                    unfinishedWaybill = ListItems.FirstOrDefault(item =>
                        item.ItemType == WeighingListItemType.Waybill &&
                        item.OrderType != OrderTypeEnum.Completed);
                }

                if (unfinishedWaybill != null)
                {
                    SelectedListItem = unfinishedWaybill;
                    SelectViewForItem(unfinishedWaybill);
                    return;
                }
            }

            // 优先级 2：未完成 WeighingRecord
            var unfinishedRecord = ListItems.FirstOrDefault(item =>
                item.ItemType == WeighingListItemType.WeighingRecord &&
                item.OrderType != OrderTypeEnum.Completed);

            if (unfinishedRecord != null)
            {
                // 按需切换标签页
                if (!IsShowAllRecords && (IsShowCompleted || !IsShowUnmatched))
                {
                    IsShowUnmatched = true;
                    IsShowCompleted = false;
                    IsShowAllRecords = false;
                    CurrentPage = 1;
                    await RefreshAsync();

                    unfinishedRecord = ListItems.FirstOrDefault(item =>
                        item.ItemType == WeighingListItemType.WeighingRecord &&
                        item.OrderType != OrderTypeEnum.Completed);
                }

                if (unfinishedRecord != null)
                {
                    SelectedListItem = unfinishedRecord;
                    SelectViewForItem(unfinishedRecord);
                    return;
                }
            }

            // 兜底：所有条目已完成，选择最新已完成项
            await SelectLatestCompletedItemAsync();
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "SelectNextUnfinishedItemAsync 失败");
        }
    }

    /// <summary>
    ///     选择下一条未匹配的项目，如果未匹配数据为空则选择已完成的第一个数据
    /// </summary>
    private async Task SelectUnmatchedNextItemAsync()
    {
        try
        {
            // 获取当前页中所有未匹配的数据
            var unmatchedItems = ListItems
                .Where(item => item.OrderType != OrderTypeEnum.Completed)
                .ToList();

            if (unmatchedItems.Count > 0)
            {
                // 如果当前页有未匹配的数据，选择下一条
                WeighingListItemDto? nextItem = null;

                if (SelectedListItem != null)
                {
                    // 如果当前有选中的项，找到当前选中项的下一条未匹配项
                    var currentIndex = unmatchedItems.FindIndex(item => item.Id == SelectedListItem.Id);
                    if (currentIndex >= 0 && currentIndex < unmatchedItems.Count - 1)
                    {
                        // 找到当前项的下一条
                        nextItem = unmatchedItems[currentIndex + 1];
                    }
                    else if (currentIndex < 0)
                    {
                        // 当前选中的项不在未匹配列表中，选择第一条未匹配项
                        nextItem = unmatchedItems.FirstOrDefault();
                    }
                    // 如果 currentIndex 是最后一项，nextItem 保持为 null，将进入 SelectLatestCompletedItemAsync
                }
                else
                {
                    // 如果没有选中的项，选择第一条未匹配项
                    nextItem = unmatchedItems.FirstOrDefault();
                }

                if (nextItem != null)
                {
                    SelectedListItem = nextItem;
                    // 如果 DetailView 正在显示，需要更新 DetailViewModel
                    if (!IsShowingMainView && DetailViewModel != null)
                    {
                        // 如果 DetailViewModel 已存在，更新其数据
                        DetailViewModel.InitializeData(nextItem, CapturedBillPhotoPath);
                    }
                    else if (nextItem is not
                             { ItemType: WeighingListItemType.Waybill, OrderType: OrderTypeEnum.Completed })
                    {
                        // 如果不是已完成的 Waybill，打开详情视图
                        _ = OpenDetail(nextItem);
                    }

                    return;
                }
            }

            // 如果当前页没有未匹配的数据，或者所有未匹配数据都已处理完，则选择已完成的第一个数据
            await SelectLatestCompletedItemAsync();
        }
        catch
        {
            // 如果出错，忽略错误，不影响主流程
        }
    }

    [ReactiveCommand]
    private async Task TakeBillPhotoAsync()
    {
        try
        {
            // 如果已存在BillPhoto且未在重新拍照模式，则进入重新拍照模式
            if (HasBillPhoto && !_isRetakingPhoto)
            {
                _isRetakingPhoto = true;
                this.RaisePropertyChanged(nameof(BillPhotoButtonText));
                this.RaisePropertyChanged(nameof(ShouldShowPreview));

                // 清除旧的BillPhotoPath（但保留数据库中的记录，直到保存时更新）
                BillPhotoPath = null;
                CapturedBillPhotoPath = null;

                // 启动预览
                if (IsUsbCameraOnline) await StartUsbCameraPreviewAsync();

                Logger?.LogInformation("进入重新拍照模式");
                return;
            }

            // 如果正在预览，则捕获当前帧
            var usbCameraService = _serviceProvider.GetService<IUsbCameraService>();
            if (usbCameraService == null)
            {
                Logger?.LogWarning("USB摄像头服务不可用");
                return;
            }

            if (!usbCameraService.IsPreviewing)
            {
                Logger?.LogWarning("摄像头预览未启动，无法拍照");
                return;
            }

            // 捕获当前帧
            var frameData = await usbCameraService.CaptureCurrentFrameAsync();
            if (frameData == null || frameData.Length == 0)
            {
                Logger?.LogWarning("捕获帧数据失败");
                return;
            }

            // 生成文件路径
            // FIX: Use absolute path to ensure photos are saved to application directory
            // when launched from any working directory (e.g., C:\Windows\System32)
            var now = DateTime.Now;
            var photosDir = AttachmentPathUtils.GetLocalStorageAbsolutePath(AttachType.TicketPhoto, now);
            var fileName = AttachmentPathUtils.GenerateBillPhotoFileName(now);

            // 确保目录存在
            if (!Directory.Exists(photosDir)) Directory.CreateDirectory(photosDir);

            var filePath = Path.Combine(photosDir, fileName);

            // 保存文件
            await File.WriteAllBytesAsync(filePath, frameData);

            // 更新属性
            CapturedBillPhotoPath = filePath;
            BillPhotoPath = filePath;
            _isRetakingPhoto = false;

            this.RaisePropertyChanged(nameof(HasBillPhoto));
            this.RaisePropertyChanged(nameof(BillPhotoButtonText));
            this.RaisePropertyChanged(nameof(ShouldShowPreview));

            // 停止预览（显示静态图片）
            await StopUsbCameraPreviewAsync();

            Logger?.LogInformation("拍照成功，文件已保存: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "拍照时发生错误");
        }
    }

    [ReactiveCommand]
    private async Task SyncDataAsync()
    {
        IsSyncing = true;
        var failedSteps = new List<string>();

        try
        {
            // 1. 物料同步
            try
            {
                await _syncMaterialService.SyncMaterialAsync();
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "物料同步失败");
                failedSteps.Add("物料同步");
            }

            // 2. 物料类型同步
            try
            {
                await _syncMaterialService.SyncMaterialTypeAsync();
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "物料类型同步失败");
                failedSteps.Add("物料类型同步");
            }

            // 3. 供应商同步
            try
            {
                await _syncMaterialService.SyncProviderAsync();
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "供应商同步失败");
                failedSteps.Add("供应商同步");
            }

            // 4. 运单推送
            try
            {
                await _weighingMatchingService.PushWaybillAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "运单推送失败");
                failedSteps.Add("运单推送");
            }

            // 5. 附件上传
            try
            {
                await _attachmentService.SyncPendingAttachmentsToOssAsync();
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "附件上传失败");
                failedSteps.Add("附件上传");
            }
        }
        finally
        {
            IsSyncing = false;
        }

        // 构建结果摘要
        var successCount = 5 - failedSteps.Count;
        string summary;
        if (failedSteps.Count == 0)
        {
            summary = $"数据同步完成";
        }
        else
        {
            summary = $"数据同步完成：{successCount} 项成功，{failedSteps.Count} 项失败";
        }

        await ShowMessageBoxAsync(summary);
    }

    [ReactiveCommand]
    private async Task LogoutAsync()
    {
        try
        {
            var parentWin = GetParentWindow();

            // Show confirmation dialog
            var result = parentWin != null
                ? await MessageBox.ShowAsync(parentWin, "确定要退出登录吗？", "确认退出登录",
                    MessageBoxIcon.Question, MessageBoxButton.YesNo)
                : await MessageBox.ShowAsync("确定要退出登录吗？", "确认退出登录",
                    MessageBoxIcon.Question, MessageBoxButton.YesNo);
            if (result != MessageBoxResult.Yes) return;

            // Clear session and credentials (soft logout — license preserved)
            await _authenticationService.LogoutAsync();
            await _authenticationService.ClearSavedCredentialAsync();

            await _localEventBus.PublishAsync(new LogoutRequestedEventData());

            // Hide current window
            parentWin?.Hide();

            // Resolve LoginWindow from DI and set up re-login flow
            var loginWindow = _serviceProvider.GetRequiredService<LoginWindow>();
            if (loginWindow.DataContext is LoginWindowViewModel loginViewModel)
            {
                loginViewModel.ResetLoginForm();

                // Subscribe to re-login success to transition back to AttendedWeighingWindow
                loginViewModel
                    .WhenAnyValue(vm => vm.IsLoginSuccessful)
                    .Where(isSuccessful => isSuccessful)
                    .Subscribe(_ =>
                    {
                        loginWindow.Hide();
                        parentWin?.Show();
                    })
                    .DisposeWith(_disposables);
            }

            loginWindow.Show();
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Logout failed");
        }
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
    private async Task OpenSettings()
    {
        try
        {
            var parentWin = GetParentWindow();
            var settingsWindow = _serviceProvider.GetRequiredService<MaterialClient.UI.Views.SettingsWindow>();
            if (parentWin != null)
                await settingsWindow.ShowDialog(parentWin);
            else
                settingsWindow.Show();
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "打开系统设置窗口失败");
        }
    }

    [ReactiveCommand]
    private async Task OpenProjectInfo()
    {
        try
        {
            var parentWin = GetParentWindow();
            var viewModel = _serviceProvider.GetRequiredService<ProjectInfoWindowViewModel>();
            
            // Initialize data before showing window
            await viewModel.InitializeAsync();
            
            var projectInfoWindow = new ProjectInfoWindow(viewModel);
            
            if (parentWin != null)
                await projectInfoWindow.ShowDialog(parentWin);
            else
                projectInfoWindow.Show();
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "打开项目信息窗口失败");
        }
    }

    [ReactiveCommand]
    private async Task ExportSolidWaste()
    {
        // TODO: 支持标准模式导出
        try
        {
            var parentWin = GetParentWindow();
            if (parentWin == null) return;

            var settings = await _settingsService.GetSettingsAsync();
            var defaultPath = string.IsNullOrEmpty(settings.SystemSettings.ExportDefaultPath)
                ? Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                : settings.SystemSettings.ExportDefaultPath;

            var dialogVm = new ExportFilterDialogViewModel { SavePath = defaultPath };
            var dialog = new ExportFilterDialog(dialogVm);
            var result = await dialog.ShowDialog<ExportFilterDialogViewModel?>(parentWin);

            if (result is not { Confirmed: true }) return;

            var savePath = result.SavePath!;
            var fileName = $"固废运单_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            var outputPath = Path.Combine(savePath, fileName);

            var filter = new SolidWasteExportFilter
            {
                StartDate = result.StartDate,
                EndDate = result.EndDate,
                PlateNumber = string.IsNullOrWhiteSpace(result.PlateNumber) ? null : result.PlateNumber,
                GoodsName = null,
                ProviderName = null
            };

            var exportService = _serviceProvider.GetRequiredService<IExcelExportService>();
            var exportResult = await exportService.ExportSolidWasteAsync(filter, outputPath);

            if (exportResult.Success)
            {
                settings.SystemSettings.ExportDefaultPath = savePath;
                await _settingsService.SaveSettingsAsync(settings);
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (parentWin is AttendedWeighingWindow attendedWindow
                    && attendedWindow.NotificationManager != null)
                {
                    if (exportResult.Success)
                        attendedWindow.NotificationManager.Show(
                            new Avalonia.Controls.Notifications.Notification("导出成功",
                                $"已导出 {exportResult.RowCount} 条运单到 {fileName}",
                                NotificationType.Success));
                    else
                        attendedWindow.NotificationManager.Show(
                            new Avalonia.Controls.Notifications.Notification("导出失败",
                                "导出过程中发生错误，请重试",
                                NotificationType.Error));
                }
            });
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "固废运单导出失败");
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
        _ = SetDisplayModeAsync(mode);
    }

    private async Task SetDisplayModeAsync(int mode)
    {
        IsShowAllRecords = mode == 0;
        IsShowUnmatched = mode == 1;
        IsShowCompleted = mode == 2;
        CurrentPage = 1;

        await RefreshAsync();

        if (ListItems.Count > 0)
        {
            var firstItem = ListItems.First();
            SelectedListItem = firstItem;

            if (firstItem is { ItemType: WeighingListItemType.Waybill, OrderType: OrderTypeEnum.Completed })
                SelectCompletedWaybill(firstItem);
            else
                _ = OpenDetail(firstItem);
        }
        else
        {
            SelectedListItem = null;
        }
    }

    [ReactiveCommand]
    private async Task PrintSolidWasteAsync()
    {
        if (SelectedListItem == null || !CanPrintSolidWaste)
            return;

        try
        {
            if (!IsPrinterEnabled)
            {
                await ShowMessageBoxAsync("未启用打印机功能，请在系统设置中启用并选择打印机。");
                return;
            }

            if (string.IsNullOrWhiteSpace(PrinterName))
            {
                await ShowMessageBoxAsync("未选择打印机，请在系统设置中选择打印机。");
                return;
            }

            var waybill = await _weighingMatchingService.GetWaybillByIdAsync(SelectedListItem.Id);
            var dto = await _weighingMatchingService.CreateWeighingTicketDtoAsync(SelectedListItem, waybill);

            var printingService = _serviceProvider.GetRequiredService<ITicketPrintingService>();

            // Re-check printer existence at click time (avoid showing preview when printer is missing/offline).
            var installedPrinters = printingService.ListInstalledPrinters();
            var isPrinterOnline = installedPrinters.Any(p =>
                string.Equals(p, PrinterName, StringComparison.OrdinalIgnoreCase));
            IsPrinterOnline = isPrinterOnline;

            if (!isPrinterOnline)
            {
                await ShowMessageBoxAsync($"打印机不在线或未安装：{PrinterName}");
                return;
            }

            var previewPath = Path.Combine(
                Path.GetTempPath(),
                $"ticket_preview_{SelectedListItem.Id}_{DateTime.Now:yyyyMMddHHmmss}.png");

            printingService.RenderTicketToImage(dto, previewPath);

            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var parentWin = GetParentWindow();

                var previewVm = _serviceProvider.GetRequiredService<PrintPreviewViewModel>();
                previewVm.SetTicket(dto, previewPath);

                var previewWindow = new PrintPreviewWindow(previewVm);

                if (parentWin != null)
                    await previewWindow.ShowDialog(parentWin);
                else
                    previewWindow.Show();
            });

            Logger?.LogInformation("固废称重单已发送到打印机。WaybillId: {WaybillId}", waybill.Id);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "打印固废称重单失败。ListItemId: {ListItemId}", SelectedListItem.Id);
            await ShowMessageBoxAsync($"打印失败：{ex.Message}");
        }
    }

    private async Task ShowMessageBoxAsync(string message)
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var parentWin = GetParentWindow();

            if (parentWin != null)
            {
                await MessageBox.ShowAsync(parentWin, message, "提示", MessageBoxIcon.None,
                    MessageBoxButton.OK);
            }
            else
            {
                await MessageBox.ShowAsync(message, "提示", MessageBoxIcon.None, MessageBoxButton.OK);
            }
        });
    }

    private static Window? GetParentWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;

        return null;
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

    [ReactiveCommand]
    private async Task EditSolidWasteAsync()
    {
        if (!CanEditSolidWaste || SelectedListItem == null)
        {
            return;
        }

        try
        {
            // 将当前固废运单状态设置为 FirstWeight（首磅）
            await _weighingMatchingService.SetWaybillFirstWeightAsync(SelectedListItem.Id);

            // 状态更新后刷新列表并保持在 MainView，以便用户查看最新结果
            await RefreshAsync();

            // 尝试重新定位到当前运单
            var updatedItem = ListItems.FirstOrDefault(x =>
                x.ItemType == WeighingListItemType.Waybill && x.Id == SelectedListItem.Id);
            if (updatedItem != null)
            {
                SelectedListItem = updatedItem;
                SelectViewForItem(updatedItem);
            }

            await ShowMessageBoxAsync("固废运单状态已更新。");
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "更新固废运单状态失败。WaybillId: {WaybillId}", SelectedListItem.Id);
            await ShowMessageBoxAsync($"更新固废运单状态失败：{ex.Message}");
        }
    }

    #endregion
}