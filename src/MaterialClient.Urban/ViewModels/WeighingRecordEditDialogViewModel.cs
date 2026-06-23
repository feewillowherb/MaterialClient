using Avalonia.Controls;
using Avalonia.Platform.Storage;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Events;
using MaterialClient.Common.Services;
using MaterialClient.UI.ViewModels;
using MaterialClient.UI.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Volo.Abp.EventBus.Local;

namespace MaterialClient.Urban.ViewModels;

/// <summary>
///     ViewModel for WeighingRecordEditDialog - edit plate/weight and preview Lpr / UrbanPhoto during approval
/// </summary>
public partial class WeighingRecordEditDialogViewModel : ReactiveObject
{
    private readonly IAttachmentService _attachmentService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILocalEventBus _localEventBus;
    private readonly ILogger<WeighingRecordEditDialogViewModel> _logger;
    private long _weighingRecordId;
    private string? _pendingLprReplacementSourcePath;
    private bool _pendingAdoptUrbanPhotoAsLpr;
    private IDisposable? _serverApprovalSubscription;

    public Action? RequestCloseDueToServerApproval { get; set; }

    public bool ClosedDueToServerApproval { get; private set; }

    public WeighingRecordEditDialogViewModel(
        IAttachmentService attachmentService,
        IServiceProvider serviceProvider,
        ILocalEventBus localEventBus,
        ILogger<WeighingRecordEditDialogViewModel> logger)
    {
        _attachmentService = attachmentService;
        _serviceProvider = serviceProvider;
        _localEventBus = localEventBus;
        _logger = logger;

        this.WhenAnyValue(x => x.AnomalyReason, x => x.LprPhotoPath, x => x.CameraPhotoPath)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(IsLprAnomaly));
                this.RaisePropertyChanged(nameof(CanAdoptUrbanPhotoAsLpr));
            });
    }

    [Reactive] private string _plateNumber = string.Empty;
    [Reactive] private string _totalWeight = string.Empty;
    [Reactive] private string _weighingDate = string.Empty;
    [Reactive] private string? _lprPhotoPath;
    [Reactive] private string? _cameraPhotoPath;
    [Reactive] private string _lprPhotoTime = "";
    [Reactive] private string _cameraPhotoTime = "";
    [Reactive] private bool _isLprImageModified;
    [Reactive] private AnomalyReason? _anomalyReason;

    public bool IsLprAnomaly => _anomalyReason == MaterialClient.Common.Entities.Enums.AnomalyReason.CaptureFailure;

    public bool CanAdoptUrbanPhotoAsLpr =>
        string.IsNullOrEmpty(LprPhotoPath) && !string.IsNullOrEmpty(CameraPhotoPath);

    public IStorageProvider? StorageProvider { get; set; }

    public EditResult? Result { get; private set; }

    public async Task LoadPhotosAsync(long weighingRecordId)
    {
        _weighingRecordId = weighingRecordId;
        _pendingLprReplacementSourcePath = null;
        _pendingAdoptUrbanPhotoAsLpr = false;
        _isLprImageModified = false;
        ClosedDueToServerApproval = false;

        _serverApprovalSubscription?.Dispose();
        _serverApprovalSubscription = _localEventBus.Subscribe<ServerApprovalSyncedEventData>(eventData =>
        {
            if (eventData.WeighingRecordId != _weighingRecordId)
            {
                return Task.CompletedTask;
            }

            ClosedDueToServerApproval = true;
            RequestCloseDueToServerApproval?.Invoke();
            return Task.CompletedTask;
        });

        try
        {
            var attachmentsByRecord =
                await _attachmentService.GetAttachmentsByWeighingRecordIdsAsync([weighingRecordId]);

            string? lprPath = null;
            string? cameraPath = null;
            DateTime? lprTime = null;
            DateTime? cameraTime = null;

            if (attachmentsByRecord.TryGetValue(weighingRecordId, out var files))
            {
                foreach (var file in files)
                {
                    if (string.IsNullOrEmpty(file.LocalPath))
                    {
                        continue;
                    }

                    if (file.AttachType == AttachType.Lpr)
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

            LprPhotoPath = lprPath;
            CameraPhotoPath = cameraPath;
            LprPhotoTime = lprTime.HasValue ? lprTime.Value.ToString("HH:mm:ss") : "";
            CameraPhotoTime = cameraTime.HasValue ? cameraTime.Value.ToString("HH:mm:ss") : "";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load photos for weighing record {RecordId}", weighingRecordId);
            LprPhotoPath = null;
            CameraPhotoPath = null;
            LprPhotoTime = "";
            CameraPhotoTime = "";
        }
    }

    [ReactiveCommand]
    private void Save()
    {
        if (!decimal.TryParse(TotalWeight, out var weight) || weight < 0)
        {
            return;
        }

        Result = new EditResult(
            PlateNumber,
            weight,
            IsLprImageModified,
            _pendingLprReplacementSourcePath,
            _pendingAdoptUrbanPhotoAsLpr);
    }

    [ReactiveCommand]
    private void Cancel()
    {
        Result = null;
    }

    [ReactiveCommand]
    private void OpenLprImageViewer(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

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

    [ReactiveCommand]
    private void OpenCameraImageViewer(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

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

    private static readonly FilePickerFileType[] ImageFileTypes =
    [
        new("图片文件") { Patterns = ["*.jpg", "*.jpeg", "*.png", "*.bmp"] }
    ];

    [ReactiveCommand]
    private async Task ReplaceLprAsync()
    {
        if (StorageProvider == null)
        {
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择车牌识别图片",
            AllowMultiple = false,
            FileTypeFilter = ImageFileTypes
        });

        if (files.Count == 0)
        {
            return;
        }

        try
        {
            _pendingAdoptUrbanPhotoAsLpr = false;
            _pendingLprReplacementSourcePath = files[0].Path.LocalPath;
            LprPhotoPath = _pendingLprReplacementSourcePath;
            IsLprImageModified = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "替换车牌识别图片失败");
        }
    }

    [ReactiveCommand]
    private void AdoptUrbanPhotoAsLpr()
    {
        if (_weighingRecordId == 0 || !CanAdoptUrbanPhotoAsLpr || string.IsNullOrEmpty(CameraPhotoPath))
        {
            return;
        }

        _pendingAdoptUrbanPhotoAsLpr = true;
        _pendingLprReplacementSourcePath = null;
        LprPhotoPath = CameraPhotoPath;
        IsLprImageModified = true;
    }
}

/// <summary>
///     Result from the weighing record edit dialog
/// </summary>
public record EditResult(
    string PlateNumber,
    decimal TotalWeight,
    bool IsLprImageModified,
    string? PendingLprReplacementSourcePath = null,
    bool PendingAdoptUrbanPhotoAsLpr = false);
