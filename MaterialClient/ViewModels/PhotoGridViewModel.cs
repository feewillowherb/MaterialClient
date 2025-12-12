using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MaterialClient.ViewModels;

/// <summary>
/// 照片网格视图的 ViewModel，负责显示进场照片和出场照片
/// </summary>
public partial class PhotoGridViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// 当前选中的照片标签 (0 = 进场照片, 1 = 出场照片)
    /// </summary>
    [ObservableProperty] private int _selectedPhotoTabIndex = 0;

    /// <summary>
    /// 进场照片1
    /// </summary>
    [ObservableProperty] private string? _entryPhoto1;

    /// <summary>
    /// 进场照片2
    /// </summary>
    [ObservableProperty] private string? _entryPhoto2;

    /// <summary>
    /// 进场照片3
    /// </summary>
    [ObservableProperty] private string? _entryPhoto3;

    /// <summary>
    /// 进场照片4
    /// </summary>
    [ObservableProperty] private string? _entryPhoto4;

    /// <summary>
    /// 出场照片1
    /// </summary>
    [ObservableProperty] private string? _exitPhoto1;

    /// <summary>
    /// 出场照片2
    /// </summary>
    [ObservableProperty] private string? _exitPhoto2;

    /// <summary>
    /// 出场照片3
    /// </summary>
    [ObservableProperty] private string? _exitPhoto3;

    /// <summary>
    /// 出场照片4
    /// </summary>
    [ObservableProperty] private string? _exitPhoto4;

    /// <summary>
    /// 是否选中进场照片标签
    /// </summary>
    public bool IsEntryPhotoTabSelected => SelectedPhotoTabIndex == 0;

    /// <summary>
    /// 是否选中出场照片标签
    /// </summary>
    public bool IsExitPhotoTabSelected => SelectedPhotoTabIndex == 1;

    public PhotoGridViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// 切换到进场照片标签
    /// </summary>
    [RelayCommand]
    private void ShowEntryPhotos()
    {
        SelectedPhotoTabIndex = 0;
    }

    /// <summary>
    /// 切换到出场照片标签
    /// </summary>
    [RelayCommand]
    private void ShowExitPhotos()
    {
        SelectedPhotoTabIndex = 1;
    }

    partial void OnSelectedPhotoTabIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsEntryPhotoTabSelected));
        OnPropertyChanged(nameof(IsExitPhotoTabSelected));
    }

    /// <summary>
    /// 从称重记录加载照片
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

            int entryIndex = 0;
            int exitIndex = 0;

            foreach (var file in attachmentFiles)
            {
                if (string.IsNullOrEmpty(file.LocalPath))
                    continue;

                if (file.AttachType == AttachType.EntryPhoto)
                {
                    SetEntryPhoto(entryIndex++, file.LocalPath);
                }
                else if (file.AttachType == AttachType.ExitPhoto)
                {
                    SetExitPhoto(exitIndex++, file.LocalPath);
                }
            }
        }
        catch
        {
            // If service is not available, photos will remain empty
        }
    }

    /// <summary>
    /// 从运单加载照片
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

            int entryIndex = 0;
            int exitIndex = 0;

            foreach (var file in attachmentFiles)
            {
                if (string.IsNullOrEmpty(file.LocalPath))
                    continue;

                if (file.AttachType == AttachType.EntryPhoto)
                {
                    SetEntryPhoto(entryIndex++, file.LocalPath);
                }
                else if (file.AttachType == AttachType.ExitPhoto)
                {
                    SetExitPhoto(exitIndex++, file.LocalPath);
                }
            }
        }
        catch
        {
            // If service is not available, photos will remain empty
        }
    }

    /// <summary>
    /// 清空所有照片
    /// </summary>
    public void Clear()
    {
        EntryPhoto1 = null;
        EntryPhoto2 = null;
        EntryPhoto3 = null;
        EntryPhoto4 = null;
        ExitPhoto1 = null;
        ExitPhoto2 = null;
        ExitPhoto3 = null;
        ExitPhoto4 = null;
    }

    private void SetEntryPhoto(int index, string path)
    {
        switch (index)
        {
            case 0: EntryPhoto1 = path; break;
            case 1: EntryPhoto2 = path; break;
            case 2: EntryPhoto3 = path; break;
            case 3: EntryPhoto4 = path; break;
        }
    }

    private void SetExitPhoto(int index, string path)
    {
        switch (index)
        {
            case 0: ExitPhoto1 = path; break;
            case 1: ExitPhoto2 = path; break;
            case 2: ExitPhoto3 = path; break;
            case 3: ExitPhoto4 = path; break;
        }
    }
}
