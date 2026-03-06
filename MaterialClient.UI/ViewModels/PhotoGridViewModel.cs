using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Models;
using MaterialClient.Common.Services;
using MaterialClient.Common.Utils;
using MaterialClient.Views;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.UI.ViewModels;

/// <summary>
///     照片网格视图的 ViewModel，负责显示进场照片和出场照片
/// </summary>
public partial class PhotoGridViewModel : ViewModelBase, ITransientDependency
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    ///     进场照片1
    /// </summary>
    [Reactive] private string? _entryPhoto1;

    /// <summary>
    ///     进场照片2
    /// </summary>
    [Reactive] private string? _entryPhoto2;

    /// <summary>
    ///     进场照片3
    /// </summary>
    [Reactive] private string? _entryPhoto3;

    /// <summary>
    ///     进场照片4
    /// </summary>
    [Reactive] private string? _entryPhoto4;

    /// <summary>
    ///     出场照片1
    /// </summary>
    [Reactive] private string? _exitPhoto1;

    /// <summary>
    ///     出场照片2
    /// </summary>
    [Reactive] private string? _exitPhoto2;

    /// <summary>
    ///     出场照片3
    /// </summary>
    [Reactive] private string? _exitPhoto3;

    /// <summary>
    ///     出场照片4
    /// </summary>
    [Reactive] private string? _exitPhoto4;

    /// <summary>
    ///     进场照片1 缩略图（异步加载，null 时显示占位图）
    /// </summary>
    [Reactive] private Bitmap? _entryPhoto1Thumbnail;

    [Reactive] private Bitmap? _entryPhoto2Thumbnail;
    [Reactive] private Bitmap? _entryPhoto3Thumbnail;
    [Reactive] private Bitmap? _entryPhoto4Thumbnail;
    [Reactive] private Bitmap? _exitPhoto1Thumbnail;
    [Reactive] private Bitmap? _exitPhoto2Thumbnail;
    [Reactive] private Bitmap? _exitPhoto3Thumbnail;
    [Reactive] private Bitmap? _exitPhoto4Thumbnail;

    /// <summary>
    ///     当前选中的照片标签 (0 = 进场照片, 1 = 出场照片)
    /// </summary>
    [Reactive] private int _selectedPhotoTabIndex;

    public PhotoGridViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;

        this.WhenAnyValue(x => x.SelectedPhotoTabIndex)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(IsEntryPhotoTabSelected));
                this.RaisePropertyChanged(nameof(IsExitPhotoTabSelected));
            });
    }

    /// <summary>
    ///     是否选中进场照片标签
    /// </summary>
    public bool IsEntryPhotoTabSelected => SelectedPhotoTabIndex == 0;

    /// <summary>
    ///     是否选中出场照片标签
    /// </summary>
    public bool IsExitPhotoTabSelected => SelectedPhotoTabIndex == 1;

    /// <summary>
    ///     切换到进场照片标签
    /// </summary>
    [ReactiveCommand]
    private void ShowEntryPhotos()
    {
        SelectedPhotoTabIndex = 0;
    }

    /// <summary>
    ///     切换到出场照片标签
    /// </summary>
    [ReactiveCommand]
    private void ShowExitPhotos()
    {
        SelectedPhotoTabIndex = 1;
    }

    /// <summary>
    ///     打开图片查看窗口
    /// </summary>
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

    /// <summary>
    ///     从称重记录加载照片
    /// </summary>
    public async Task LoadFromWeighingRecordAsync(WeighingRecord record)
    {
        try
        {
            Clear();

            var attachmentService = _serviceProvider.GetService<IAttachmentService>();
            if (attachmentService == null) return;

            var attachmentsDict = await attachmentService.GetAttachmentsByWeighingRecordIdsAsync(new[] { record.Id });
            if (!attachmentsDict.TryGetValue(record.Id, out var attachmentFiles))
                return;

            var entryIndex = 0;
            var exitIndex = 0;

            foreach (var file in attachmentFiles)
            {
                if (string.IsNullOrEmpty(file.LocalPath))
                    continue;

                if (file.AttachType == AttachType.EntryPhoto)
                    SetEntryPhoto(entryIndex++, file.LocalPath);
                else if (file.AttachType == AttachType.ExitPhoto) SetExitPhoto(exitIndex++, file.LocalPath);
            }
        }
        catch
        {
            // If service is not available, photos will remain empty
        }
    }

    /// <summary>
    ///     从运单加载照片
    /// </summary>
    public async Task LoadFromWaybillAsync(Waybill waybill)
    {
        try
        {
            Clear();

            var attachmentService = _serviceProvider.GetService<IAttachmentService>();
            if (attachmentService == null) return;

            var attachmentsDict = await attachmentService.GetAttachmentsByWaybillIdsAsync(new[] { waybill.Id });
            if (!attachmentsDict.TryGetValue(waybill.Id, out var attachmentFiles))
                return;

            var entryIndex = 0;
            var exitIndex = 0;

            foreach (var file in attachmentFiles)
            {
                if (string.IsNullOrEmpty(file.LocalPath))
                    continue;

                if (file.AttachType == AttachType.EntryPhoto)
                    SetEntryPhoto(entryIndex++, file.LocalPath);
                else if (file.AttachType == AttachType.ExitPhoto) SetExitPhoto(exitIndex++, file.LocalPath);
            }
        }
        catch
        {
            // If service is not available, photos will remain empty
        }
    }

    /// <summary>
    ///     从列表项加载照片（统一接口，根据 ItemType 自动路由）
    /// </summary>
    public async Task LoadFromListItemAsync(WeighingListItemDto item)
    {
        try
        {
            Clear();

            var attachmentService = _serviceProvider.GetService<IAttachmentService>();
            if (attachmentService == null) return;

            var attachmentFiles = await attachmentService.GetAttachmentsByListItemAsync(item);

            var entryIndex = 0;
            var exitIndex = 0;

            foreach (var file in attachmentFiles)
            {
                if (string.IsNullOrEmpty(file.LocalPath))
                    continue;

                if (file.AttachType == AttachType.EntryPhoto || file.AttachType == AttachType.UnmatchedEntryPhoto)
                    SetEntryPhoto(entryIndex++, file.LocalPath);
                else if (file.AttachType == AttachType.ExitPhoto) SetExitPhoto(exitIndex++, file.LocalPath);
            }
        }
        catch
        {
            // If service is not available, photos will remain empty
        }
    }

    /// <summary>
    ///     清空所有照片
    /// </summary>
    public void Clear()
    {
        DisposeAndClearThumbnail(() => EntryPhoto1Thumbnail, v => EntryPhoto1Thumbnail = v);
        DisposeAndClearThumbnail(() => EntryPhoto2Thumbnail, v => EntryPhoto2Thumbnail = v);
        DisposeAndClearThumbnail(() => EntryPhoto3Thumbnail, v => EntryPhoto3Thumbnail = v);
        DisposeAndClearThumbnail(() => EntryPhoto4Thumbnail, v => EntryPhoto4Thumbnail = v);
        DisposeAndClearThumbnail(() => ExitPhoto1Thumbnail, v => ExitPhoto1Thumbnail = v);
        DisposeAndClearThumbnail(() => ExitPhoto2Thumbnail, v => ExitPhoto2Thumbnail = v);
        DisposeAndClearThumbnail(() => ExitPhoto3Thumbnail, v => ExitPhoto3Thumbnail = v);
        DisposeAndClearThumbnail(() => ExitPhoto4Thumbnail, v => ExitPhoto4Thumbnail = v);

        EntryPhoto1 = null;
        EntryPhoto2 = null;
        EntryPhoto3 = null;
        EntryPhoto4 = null;
        ExitPhoto1 = null;
        ExitPhoto2 = null;
        ExitPhoto3 = null;
        ExitPhoto4 = null;
    }

    private static void DisposeAndClearThumbnail(Func<Bitmap?> getter, Action<Bitmap?> setter)
    {
        var old = getter();
        setter(null);
        old?.Dispose();
    }

    private const int ThumbnailDecodeWidth = 480;

    private void SetEntryPhoto(int index, string path)
    {
        switch (index)
        {
            case 0:
                EntryPhoto1 = path;
                ClearOneThumbnail(() => EntryPhoto1Thumbnail, v => EntryPhoto1Thumbnail = v);
                LoadThumbnailForSlot(isEntry: true, 0, path, () => EntryPhoto1, () => EntryPhoto1Thumbnail, v => EntryPhoto1Thumbnail = v);
                break;
            case 1:
                EntryPhoto2 = path;
                ClearOneThumbnail(() => EntryPhoto2Thumbnail, v => EntryPhoto2Thumbnail = v);
                LoadThumbnailForSlot(isEntry: true, 1, path, () => EntryPhoto2, () => EntryPhoto2Thumbnail, v => EntryPhoto2Thumbnail = v);
                break;
            case 2:
                EntryPhoto3 = path;
                ClearOneThumbnail(() => EntryPhoto3Thumbnail, v => EntryPhoto3Thumbnail = v);
                LoadThumbnailForSlot(isEntry: true, 2, path, () => EntryPhoto3, () => EntryPhoto3Thumbnail, v => EntryPhoto3Thumbnail = v);
                break;
            case 3:
                EntryPhoto4 = path;
                ClearOneThumbnail(() => EntryPhoto4Thumbnail, v => EntryPhoto4Thumbnail = v);
                LoadThumbnailForSlot(isEntry: true, 3, path, () => EntryPhoto4, () => EntryPhoto4Thumbnail, v => EntryPhoto4Thumbnail = v);
                break;
        }
    }

    private void SetExitPhoto(int index, string path)
    {
        switch (index)
        {
            case 0:
                ExitPhoto1 = path;
                ClearOneThumbnail(() => ExitPhoto1Thumbnail, v => ExitPhoto1Thumbnail = v);
                LoadThumbnailForSlot(isEntry: false, 0, path, () => ExitPhoto1, () => ExitPhoto1Thumbnail, v => ExitPhoto1Thumbnail = v);
                break;
            case 1:
                ExitPhoto2 = path;
                ClearOneThumbnail(() => ExitPhoto2Thumbnail, v => ExitPhoto2Thumbnail = v);
                LoadThumbnailForSlot(isEntry: false, 1, path, () => ExitPhoto2, () => ExitPhoto2Thumbnail, v => ExitPhoto2Thumbnail = v);
                break;
            case 2:
                ExitPhoto3 = path;
                ClearOneThumbnail(() => ExitPhoto3Thumbnail, v => ExitPhoto3Thumbnail = v);
                LoadThumbnailForSlot(isEntry: false, 2, path, () => ExitPhoto3, () => ExitPhoto3Thumbnail, v => ExitPhoto3Thumbnail = v);
                break;
            case 3:
                ExitPhoto4 = path;
                ClearOneThumbnail(() => ExitPhoto4Thumbnail, v => ExitPhoto4Thumbnail = v);
                LoadThumbnailForSlot(isEntry: false, 3, path, () => ExitPhoto4, () => ExitPhoto4Thumbnail, v => ExitPhoto4Thumbnail = v);
                break;
        }
    }

    private static void ClearOneThumbnail(Func<Bitmap?> getter, Action<Bitmap?> setter)
    {
        var old = getter();
        setter(null);
        old?.Dispose();
    }

    private void LoadThumbnailForSlot(
        bool isEntry,
        int index,
        string path,
        Func<string?> getCurrentPath,
        Func<Bitmap?> getCurrentThumbnail,
        Action<Bitmap?> setThumbnail)
    {
        if (string.IsNullOrEmpty(path))
            return;

        var pathWeLoaded = path;
        _ = Task.Run(() => LoadThumbnailBitmap(pathWeLoaded))
            .ContinueWith(t =>
            {
                var bitmap = t.Status == TaskStatus.RanToCompletion ? t.Result : null;
                Dispatcher.UIThread.Post(() =>
                {
                    if (getCurrentPath() != pathWeLoaded)
                    {
                        bitmap?.Dispose();
                        return;
                    }

                    var old = getCurrentThumbnail();
                    setThumbnail(bitmap);
                    old?.Dispose();
                });
            }, TaskContinuationOptions.None);
    }

    private static Bitmap? LoadThumbnailBitmap(string path)
    {
        try
        {
            var absolutePath = PathManager.ToAbsolutePath(path);
            if (!File.Exists(absolutePath))
                return null;
            using var stream = File.OpenRead(absolutePath);
            return Bitmap.DecodeToWidth(stream, ThumbnailDecodeWidth);
        }
        catch
        {
            return null;
        }
    }
}