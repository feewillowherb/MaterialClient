using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
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
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Events;
using MaterialClient.Common.Models;
using MaterialClient.Common.Services;
using MaterialClient.Common.Services.Hardware;
using MaterialClient.Common.Services.Hikvision;
using MaterialClient.Common.Utils;
using MaterialClient.Views;
using MaterialClient.Views.AttendedWeighing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.ViewModels;

public partial class AttendedWeighingViewModel : ViewModelBase, IDisposable, ITransientDependency
{
    private readonly IAttendedWeighingService? _attendedWeighingService;
    private readonly CompositeDisposable _disposables = new();
    private readonly IServiceProvider _serviceProvider;
    private readonly ITruckScaleWeightService _truckScaleWeightService;
    private readonly IWeighingMatchingService _weighingMatchingService;
    private AttendedWeighingStatus _currentWeighingStatus = AttendedWeighingStatus.OffScale;
    private DispatcherTimer? _notificationFadeOutTimer;
    private readonly TextBlock _notificationTextBlockHolder = new();

    public AttendedWeighingViewModel(
        IWeighingMatchingService weighingMatchingService,
        IServiceProvider serviceProvider,
        ITruckScaleWeightService truckScaleWeightService,
        IAttendedWeighingService attendedWeighingService
    ) : base(serviceProvider.GetService<ILogger<AttendedWeighingViewModel>>())
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
                    if (IsUsbCameraOnline && ShouldShowPreview)
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

        this.WhenAnyValue(x => x.IsReceiving)
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
                    if (IsUsbCameraOnline && ShouldShowPreview) await StartUsbCameraPreviewAsync();
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
        StartUsbCameraStatusCheckTimer();
        _ = LoadPrinterSettingsAsync();
        StartPrinterStatusCheckTimer();
        _ = StartAllDevicesAsync();

        // Initialize state from service
        if (_attendedWeighingService != null)
        {
            _currentWeighingStatus = _attendedWeighingService.GetCurrentStatus();
            MostFrequentPlateNumber = _attendedWeighingService.GetMostFrequentPlateNumber();
            IsReceiving = _attendedWeighingService.CurrentDeliveryType == DeliveryType.Receiving;
        }

        // Start MessageBus subscriptions
        StartStatusChangedMessageBusSubscription();
        StartPlateNumberChangedMessageBusSubscription();
        StartWeighingRecordCreatedMessageBusSubscription();
        StartDeliveryTypeChangedMessageBusSubscription();
        StartMatchSucceededMessageBusSubscription();
        StartSaveCompletedMessageBusSubscription();
        StartUpdatePlateNumberMessageBusSubscription();
        StartSettingsSavedMessageBusSubscription();

        this.WhenAnyValue(x => x.PrinterName)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(PrinterTooltip)))
            .DisposeWith(_disposables);

        // 监听 USB 摄像头在线状态变化、ShouldShowPreview变化和IsShowingMainView变化，自动启动/停止预览
        this.WhenAnyValue(x => x.IsUsbCameraOnline, x => x.ShouldShowPreview, x => x.IsShowingMainView)
            .DistinctUntilChanged()
            .Subscribe(async tuple =>
            {
                var (isOnline, shouldShow, isShowingMainView) = tuple;
                if (isOnline && shouldShow && !isShowingMainView)
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
    ///     页面首次加载时的初始化逻辑
    /// </summary>
    public async Task InitializeOnFirstLoadAsync()
    {
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

    private async Task LoadPrinterSettingsAsync()
    {
        try
        {
            var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
            var settings = await settingsService.GetSettingsAsync();

            IsPrinterEnabled = settings.SystemSettings.EnablePrinter;
            PrinterName = settings.SystemSettings.SelectedPrinterName ?? string.Empty;

            if (IsPrinterEnabled)
                await CheckPrinterStatusOnceAsync();
            else
                Dispatcher.UIThread.Post(() => { IsPrinterOnline = false; });
        }
        catch (Exception ex)
        {
            Logger?.LogWarning(ex, "Failed to load printer settings");
            Dispatcher.UIThread.Post(() =>
            {
                IsPrinterEnabled = false;
                IsPrinterOnline = false;
            });
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

    private async Task StartUsbCameraPreviewAsync()
    {
        try
        {
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

    private void StartTimeUpdateTimer()
    {
        var timeTimer = new Timer(_ => CurrentTime = DateTime.Now, null,
            TimeSpan.Zero, TimeSpan.FromSeconds(1));
        _disposables.Add(timeTimer);
    }


    /// <summary>
    ///     订阅状态变化消息（通过 ReactiveUI MessageBus）
    /// </summary>
    private void StartStatusChangedMessageBusSubscription()
    {
        MessageBus.Current.Listen<StatusChangedMessage>()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(message =>
            {
                _currentWeighingStatus = message.Status;
                this.RaisePropertyChanged(nameof(CurrentWeighingStatusText));
                this.RaisePropertyChanged(nameof(IsWeighingActive));
            })
            .DisposeWith(_disposables);
    }

    /// <summary>
    ///     订阅车牌号变化消息（通过 ReactiveUI MessageBus）
    /// </summary>
    private void StartPlateNumberChangedMessageBusSubscription()
    {
        MessageBus.Current.Listen<PlateNumberChangedMessage>()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(message => { MostFrequentPlateNumber = message.PlateNumber; })
            .DisposeWith(_disposables);
    }

    /// <summary>
    ///     订阅称重记录创建消息（通过 ReactiveUI MessageBus）
    /// </summary>
    private void StartWeighingRecordCreatedMessageBusSubscription()
    {
        MessageBus.Current.Listen<WeighingRecordCreatedMessage>()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(async message =>
            {
                Logger?.LogInformation("接收到新称重记录创建事件, ID: {WeighingRecordId}", message.WeighingRecordId);
                await RefreshAsync();
            })
            .DisposeWith(_disposables);
    }

    /// <summary>
    ///     订阅收发料类型变化消息（通过 ReactiveUI MessageBus）
    /// </summary>
    private void StartDeliveryTypeChangedMessageBusSubscription()
    {
        MessageBus.Current.Listen<DeliveryTypeChangedMessage>()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(message =>
            {
                IsReceiving = message.DeliveryType == DeliveryType.Receiving;
                ShowDeliveryTypeChangedNotification(message.DeliveryType);
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
    ///     订阅匹配成功消息（通过 ReactiveUI MessageBus）
    /// </summary>
    private void StartMatchSucceededMessageBusSubscription()
    {
        MessageBus.Current.Listen<MatchSucceededMessage>()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(async message =>
            {
                Logger?.LogInformation(
                    "AttendedWeighingViewModel: Received MatchSucceededMessage for WaybillId {WaybillId}, WeighingRecordId {RecordId}",
                    message.WaybillId, message.WeighingRecordId);

                try
                {
                    // 刷新列表
                    await RefreshAsync();

                    // 查找匹配成功的 Waybill 列表项
                    var matchedItem = ListItems
                        .FirstOrDefault(item =>
                            item.ItemType == WeighingListItemType.Waybill &&
                            item.Id == message.WaybillId);

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
                            message.WaybillId);
                    }
                    else
                    {
                        Logger?.LogWarning(
                            "AttendedWeighingViewModel: Matched Waybill {WaybillId} not found in current list",
                            message.WaybillId);
                    }
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex,
                        "AttendedWeighingViewModel: Error while handling MatchSucceededMessage for WaybillId {WaybillId}",
                        message.WaybillId);
                }
            })
            .DisposeWith(_disposables);
    }

    /// <summary>
    ///     订阅保存完成消息（通过 ReactiveUI MessageBus）
    /// </summary>
    private void StartSaveCompletedMessageBusSubscription()
    {
        MessageBus.Current.Listen<SaveCompletedMessage>()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(async message =>
            {
                Logger?.LogInformation(
                    "AttendedWeighingViewModel: Received SaveCompletedMessage for ItemId {ItemId}, ItemType {ItemType}",
                    message.ItemId, message.ItemType);

                try
                {
                    // 刷新列表
                    await RefreshAsync();

                    // 查找保存的列表项
                    var savedItem = ListItems
                        .FirstOrDefault(item =>
                            item.ItemType == message.ItemType &&
                            item.Id == message.ItemId);

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
                            message.ItemId, message.ItemType);
                    }
                    else
                    {
                        Logger?.LogWarning(
                            "AttendedWeighingViewModel: Saved item {ItemId} of type {ItemType} not found in current list",
                            message.ItemId, message.ItemType);
                    }
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex,
                        "AttendedWeighingViewModel: Error while handling SaveCompletedMessage for ItemId {ItemId}",
                        message.ItemId);
                }
            })
            .DisposeWith(_disposables);
    }

    /// <summary>
    ///     订阅更新车牌号消息（通过 ReactiveUI MessageBus）
    /// </summary>
    private void StartUpdatePlateNumberMessageBusSubscription()
    {
        MessageBus.Current.Listen<UpdatePlateNumberMessage>()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(async message =>
            {
                Logger?.LogInformation(
                    "AttendedWeighingViewModel: Received UpdatePlateNumberMessage for WeighingRecordId {RecordId}, PlateNumber {PlateNumber}",
                    message.WeighingRecordId, message.PlateNumber);

                try
                {
                    // 刷新列表以更新车牌号
                    await RefreshAsync();

                    Logger?.LogInformation(
                        "AttendedWeighingViewModel: Refreshed list after plate number update for WeighingRecordId {RecordId}",
                        message.WeighingRecordId);
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex,
                        "AttendedWeighingViewModel: Error while handling UpdatePlateNumberMessage for WeighingRecordId {RecordId}",
                        message.WeighingRecordId);
                }
            })
            .DisposeWith(_disposables);
    }

    /// <summary>
    ///     订阅设置已保存消息（通过 ReactiveUI MessageBus）
    /// </summary>
    private void StartSettingsSavedMessageBusSubscription()
    {
        MessageBus.Current.Listen<SettingsSavedMessage>()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(async _ =>
            {
                Logger?.LogInformation(
                    "AttendedWeighingViewModel: Received SettingsSavedMessage, checking camera status");

                try
                {
                    await CheckCameraStatusOnceAsync();
                    await LoadPrinterSettingsAsync();
                    Logger?.LogInformation(
                        "AttendedWeighingViewModel: Camera status check completed after settings save");
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex,
                        "AttendedWeighingViewModel: Error while checking camera status after settings save");
                }
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
            AttendedWeighingStatus.WaitingForDeparture => "等待下磅",
            _ => "未知状态"
        };
    }

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

    [Reactive] private string? _joinWeightInfo;

    [Reactive] private string? _outWeightInfo;

    [Reactive] private bool _isScaleOnline;

    [Reactive] private bool _isCameraOnline;

    [Reactive] private bool _isUsbCameraOnline;

    [Reactive] private bool _isPrinterEnabled;

    [Reactive] private bool _isPrinterOnline;

    [Reactive] private string _printerName = string.Empty;

    [Reactive] private Bitmap? _usbCameraPreview;

    [Reactive] private ObservableCollection<CameraStatusViewModel> _cameraStatuses = new();

    public bool HasCameraStatuses => CameraStatuses.Count > 0;

    [Reactive] private string? _mostFrequentPlateNumber;

    [Reactive] private bool _isShowingMainView = true;

    public bool IsShowingDetailView => !IsShowingMainView;

    public InlineCollection DeliveryTypeNotificationInlines => _notificationTextBlockHolder.Inlines;

    [Reactive] private double _deliveryTypeNotificationOpacity;

    [Reactive] private AttendedWeighingDetailViewModel? _detailViewModel;

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

    public bool IsCompletedWaybillSelected => SelectedListItem is
        { ItemType: WeighingListItemType.Waybill, OrderType: OrderTypeEnum.Completed };

    public bool CanPrintSolidWaste => SelectedListItem is
    {
        ItemType: WeighingListItemType.Waybill, OrderType: OrderTypeEnum.Completed,
        WeighingMode: WeighingMode.SolidWaste
    };

    public string DeliveryTypeTitleText => IsReceiving ? "收料信息" : "发料信息";

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
        JoinWeightInfo = null;
        OutWeightInfo = null;
    }

    [ReactiveCommand]
    private Task OpenDetail(WeighingListItemDto? item)
    {
        if (item == null) return Task.CompletedTask;

        try
        {
            DetailViewModel = _serviceProvider.GetRequiredService<AttendedWeighingDetailViewModel>();
            DetailViewModel.InitializeData(item, CapturedBillPhotoPath);

            DetailViewModel.SaveCompleted += OnDetailSaveCompleted;
            DetailViewModel.AbolishCompleted += OnDetailAbolishCompleted;
            DetailViewModel.CloseRequested += OnDetailCloseRequested;
            DetailViewModel.MatchCompleted += OnDetailMatchCompleted;
            DetailViewModel.CompleteCompleted += OnDetailCompleteCompleted;
            DetailViewModel.ManualMatchSaveCompleted += OnDetailManualMatchSaveCompleted;

            IsShowingMainView = false;
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "打开详情视图失败");
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
            DetailViewModel.ManualMatchSaveCompleted -= OnDetailManualMatchSaveCompleted;
        }

        SelectedListItem = null;
        IsShowingMainView = true;
        DetailViewModel = null;
    }

    private async void OnDetailSaveCompleted(object? sender, ItemOperationCompletedEventArgs e)
    {
        await NavigateToItemAsync(e);
    }

    private async void OnDetailAbolishCompleted(object? sender, ItemOperationCompletedEventArgs e)
    {
        // Abolish操作删除了item，所以导航到下一个未匹配项
        await RefreshAsync();
        await SelectUnmatchedNextItemAsync();
    }

    private async void OnDetailMatchCompleted(object? sender, ItemOperationCompletedEventArgs e)
    {
        await NavigateToItemAsync(e);
    }

    private async void OnDetailCompleteCompleted(object? sender, ItemOperationCompletedEventArgs e)
    {
        await NavigateToItemAsync(e);
    }

    private async void OnDetailManualMatchSaveCompleted(object? sender, ItemOperationCompletedEventArgs e)
    {
        await NavigateToItemAsync(e);
    }

    private async void OnDetailCloseRequested(object? sender, EventArgs e)
    {
        await RefreshAsync();
        //BackToMain();
        await SelectUnmatchedNextItemAsync();
    }

    /// <summary>
    ///     统一的导航逻辑：根据操作上下文导航到目标项
    /// </summary>
    private async Task NavigateToItemAsync(ItemOperationCompletedEventArgs args)
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
                if (args.OperationType == "Complete")
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
    private bool ShouldSwitchTab(ItemOperationCompletedEventArgs args)
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
    private void SwitchToAppropriateTab(ItemOperationCompletedEventArgs args)
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
            var now = DateTime.Now;
            var photosDir = AttachmentPathUtils.GetLocalStoragePath(AttachType.TicketPhoto, now);
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
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var parentWin = GetParentWindow();

            var messageBox = MessageBoxManager.GetMessageBoxStandard(
                "提示",
                message,
                ButtonEnum.Ok,
                Icon.None);

            return parentWin != null
                ? messageBox.ShowWindowDialogAsync(parentWin)
                : messageBox.ShowAsync();
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

    #endregion
}