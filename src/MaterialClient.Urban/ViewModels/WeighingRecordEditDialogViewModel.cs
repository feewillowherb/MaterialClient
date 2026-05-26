using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Services;
using MaterialClient.UI.ViewModels;
using MaterialClient.UI.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace MaterialClient.Urban.ViewModels;

/// <summary>
///     ViewModel for WeighingRecordEditDialog - edit plate/weight and preview LRP / UrbanPhoto during approval
/// </summary>
public partial class WeighingRecordEditDialogViewModel : ReactiveObject
{
    private readonly IAttachmentService _attachmentService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WeighingRecordEditDialogViewModel> _logger;

    public WeighingRecordEditDialogViewModel(
        IAttachmentService attachmentService,
        IServiceProvider serviceProvider,
        ILogger<WeighingRecordEditDialogViewModel> logger)
    {
        _attachmentService = attachmentService;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    [Reactive] private string _plateNumber = string.Empty;
    [Reactive] private string _totalWeight = string.Empty;
    [Reactive] private string? _lprPhotoPath;
    [Reactive] private string? _cameraPhotoPath;
    [Reactive] private string _lprPhotoTime = "";
    [Reactive] private string _cameraPhotoTime = "";

    public EditResult? Result { get; private set; }

    public async Task LoadPhotosAsync(long weighingRecordId)
    {
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

        Result = new EditResult(PlateNumber, weight);
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
}

/// <summary>
///     Result from the weighing record edit dialog
/// </summary>
public record EditResult(string PlateNumber, decimal TotalWeight);
