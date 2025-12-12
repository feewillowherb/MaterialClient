using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Domain.Repositories;

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

            var attachmentRepository = _serviceProvider.GetService<IRepository<WeighingRecordAttachment, int>>();
            if (attachmentRepository == null) return;

            var attachments = await attachmentRepository.GetListAsync(
                predicate: x => x.WeighingRecordId == record.Id,
                includeDetails: true
            );

            int entryIndex = 0;
            int exitIndex = 0;

            foreach (var attachment in attachments)
            {
                if (attachment.AttachmentFile == null || string.IsNullOrEmpty(attachment.AttachmentFile.LocalPath))
                    continue;

                var localPath = attachment.AttachmentFile.LocalPath;

                if (attachment.AttachmentFile.AttachType == AttachType.EntryPhoto)
                {
                    SetEntryPhoto(entryIndex++, localPath);
                }
                else if (attachment.AttachmentFile.AttachType == AttachType.ExitPhoto)
                {
                    SetExitPhoto(exitIndex++, localPath);
                }
            }
        }
        catch
        {
            // If repository is not available, photos will remain empty
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

            var waybillAttachmentRepository = _serviceProvider.GetService<IRepository<WaybillAttachment, int>>();
            if (waybillAttachmentRepository == null) return;

            var attachments = await waybillAttachmentRepository.GetListAsync(
                predicate: x => x.WaybillId == waybill.Id,
                includeDetails: true
            );

            int entryIndex = 0;
            int exitIndex = 0;

            foreach (var attachment in attachments)
            {
                if (attachment.AttachmentFile == null || string.IsNullOrEmpty(attachment.AttachmentFile.LocalPath))
                    continue;

                var localPath = attachment.AttachmentFile.LocalPath;

                if (attachment.AttachmentFile.AttachType == AttachType.EntryPhoto)
                {
                    SetEntryPhoto(entryIndex++, localPath);
                }
                else if (attachment.AttachmentFile.AttachType == AttachType.ExitPhoto)
                {
                    SetExitPhoto(exitIndex++, localPath);
                }
            }
        }
        catch
        {
            // If repository is not available, photos will remain empty
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
