using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Services;
using Volo.Abp.Domain.Repositories;

namespace MaterialClient.ViewModels;

public partial class AttendedWeighingViewModel : ViewModelBase
{
    private readonly IRepository<WeighingRecord, long>? _weighingRecordRepository;
    private readonly IRepository<Waybill, long>? _waybillRepository;

    [ObservableProperty]
    private ObservableCollection<WeighingRecord> unmatchedWeighingRecords = new();

    [ObservableProperty]
    private ObservableCollection<Waybill> completedWaybills = new();

    [ObservableProperty]
    private WeighingRecord? selectedWeighingRecord;

    [ObservableProperty]
    private Waybill? selectedWaybill;

    [ObservableProperty]
    private ObservableCollection<string> vehiclePhotos = new();

    [ObservableProperty]
    private string? billPhotoPath;

    public bool IsWeighingRecordSelected => SelectedWeighingRecord != null && SelectedWaybill == null;
    public bool IsWaybillSelected => SelectedWaybill != null && SelectedWeighingRecord == null;

    public ICommand RefreshCommand { get; }

    private Timer? _autoRefreshTimer;
    private const int AutoRefreshIntervalMs = 5000; // Refresh every 5 seconds

    public AttendedWeighingViewModel()
    {
        // Try to get repositories from service locator (may be null if ABP not initialized yet)
        try
        {
            _weighingRecordRepository = ServiceLocator.GetService<IRepository<WeighingRecord, long>>();
            _waybillRepository = ServiceLocator.GetService<IRepository<Waybill, long>>();
        }
        catch
        {
            // ServiceLocator not initialized yet, will retry when Refresh is called
        }

        RefreshCommand = new RelayCommand(async () => await RefreshAsync());
        
        // Load initial data
        _ = RefreshAsync();

        // Start auto-refresh timer to reflect matching results in real-time
        StartAutoRefresh();
    }

    private void StartAutoRefresh()
    {
        _autoRefreshTimer = new Timer(async _ => await RefreshAsync(), null, 
            TimeSpan.FromMilliseconds(AutoRefreshIntervalMs), 
            TimeSpan.FromMilliseconds(AutoRefreshIntervalMs));
    }

    public void StopAutoRefresh()
    {
        _autoRefreshTimer?.Dispose();
        _autoRefreshTimer = null;
    }

    partial void OnSelectedWeighingRecordChanged(WeighingRecord? value)
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
        OnPropertyChanged(nameof(IsWeighingRecordSelected));
        OnPropertyChanged(nameof(IsWaybillSelected));
    }

    partial void OnSelectedWaybillChanged(Waybill? value)
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
        OnPropertyChanged(nameof(IsWeighingRecordSelected));
        OnPropertyChanged(nameof(IsWaybillSelected));
    }

    private async Task LoadWeighingRecordPhotos(WeighingRecord record)
    {
        try
        {
            var attachmentRepository = ServiceLocator.GetService<IRepository<WeighingRecordAttachment, int>>();
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
            var waybillAttachmentRepository = ServiceLocator.GetService<IRepository<WaybillAttachment, int>>();
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

    private async Task RefreshAsync()
    {
        try
        {
            if (_weighingRecordRepository != null)
            {
                // Load unmatched weighing records (RecordType == Unmatch)
                var allRecords = await _weighingRecordRepository.GetListAsync();
                var unmatchedRecords = allRecords
                    .Where(x => x.RecordType == WeighingRecordType.Unmatch)
                    .OrderByDescending(r => r.CreationTime)
                    .ToList();

                UnmatchedWeighingRecords.Clear();
                foreach (var record in unmatchedRecords)
                {
                    UnmatchedWeighingRecords.Add(record);
                }
            }

            if (_waybillRepository != null)
            {
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
            }
        }
        catch
        {
            // If repositories are not available, collections will remain empty
            // This allows the UI to work even before ABP is fully initialized
        }
    }
}
