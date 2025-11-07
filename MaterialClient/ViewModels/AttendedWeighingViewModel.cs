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

    // New properties for the updated UI
    [ObservableProperty]
    private DateTime currentTime = DateTime.Now;

    [ObservableProperty]
    private decimal currentWeight = 0.00m;

    [ObservableProperty]
    private bool isReceiving = true;

    [ObservableProperty]
    private bool showAllRecords = true;

    [ObservableProperty]
    private bool showUnmatched = false;

    [ObservableProperty]
    private bool showCompleted = false;

    [ObservableProperty]
    private ObservableCollection<object> displayRecords = new();

    [ObservableProperty]
    private object? selectedRecord;

    // Photo properties for current photos
    [ObservableProperty]
    private string? currentEntryPhoto1;

    [ObservableProperty]
    private string? currentEntryPhoto2;

    [ObservableProperty]
    private string? currentEntryPhoto3;

    [ObservableProperty]
    private string? currentEntryPhoto4;

    // Photo properties for previous vehicle
    [ObservableProperty]
    private string? entryPhoto1;

    [ObservableProperty]
    private string? entryPhoto2;

    [ObservableProperty]
    private string? entryPhoto3;

    [ObservableProperty]
    private string? entryPhoto4;

    [ObservableProperty]
    private string? exitPhoto1;

    [ObservableProperty]
    private string? exitPhoto2;

    [ObservableProperty]
    private string? exitPhoto3;

    [ObservableProperty]
    private string? exitPhoto4;

    [ObservableProperty]
    private string? materialInfo;

    [ObservableProperty]
    private string? offsetInfo;

    public bool IsWeighingRecordSelected => SelectedWeighingRecord != null && SelectedWaybill == null;
    public bool IsWaybillSelected => SelectedWaybill != null && SelectedWeighingRecord == null;

    public ICommand RefreshCommand { get; }
    public ICommand SetReceivingCommand { get; }
    public ICommand SetSendingCommand { get; }
    public ICommand ShowAllRecordsCommand { get; }
    public ICommand ShowUnmatchedCommand { get; }
    public ICommand ShowCompletedCommand { get; }
    public ICommand SelectRecordCommand { get; }
    public ICommand TakeBillPhotoCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CloseCommand { get; }

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
        SetReceivingCommand = new RelayCommand(() => IsReceiving = true);
        SetSendingCommand = new RelayCommand(() => IsReceiving = false);
        ShowAllRecordsCommand = new RelayCommand(() => SetDisplayMode(0));
        ShowUnmatchedCommand = new RelayCommand(() => SetDisplayMode(1));
        ShowCompletedCommand = new RelayCommand(() => SetDisplayMode(2));
        SelectRecordCommand = new RelayCommand<object>(OnRecordSelected);
        TakeBillPhotoCommand = new RelayCommand(OnTakeBillPhoto);
        SaveCommand = new RelayCommand(OnSave);
        CloseCommand = new RelayCommand(OnClose);
        
        // Load initial data
        _ = RefreshAsync();

        // Start auto-refresh timer to reflect matching results in real-time
        StartAutoRefresh();
        StartTimeUpdateTimer();
    }

    private void SetDisplayMode(int mode)
    {
        ShowAllRecords = mode == 0;
        ShowUnmatched = mode == 1;
        ShowCompleted = mode == 2;
        UpdateDisplayRecords();
    }

    private void UpdateDisplayRecords()
    {
        DisplayRecords.Clear();
        
        if (ShowAllRecords)
        {
            foreach (var record in UnmatchedWeighingRecords)
            {
                DisplayRecords.Add(record);
            }
            foreach (var waybill in CompletedWaybills)
            {
                DisplayRecords.Add(waybill);
            }
        }
        else if (ShowUnmatched)
        {
            foreach (var record in UnmatchedWeighingRecords)
            {
                DisplayRecords.Add(record);
            }
        }
        else if (ShowCompleted)
        {
            foreach (var waybill in CompletedWaybills)
            {
                DisplayRecords.Add(waybill);
            }
        }
    }

    private void OnRecordSelected(object? record)
    {
        SelectedRecord = record;
        // TODO: Load photos for the selected record
    }

    private void OnTakeBillPhoto()
    {
        // TODO: Implement bill photo capture
    }

    private void OnSave()
    {
        // TODO: Implement save logic
    }

    private void OnClose()
    {
        // TODO: Implement close logic
    }

    private void StartTimeUpdateTimer()
    {
        var timeTimer = new Timer(_ => CurrentTime = DateTime.Now, null, 
            TimeSpan.Zero, TimeSpan.FromSeconds(1));
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
            
            // Update display records after refresh
            UpdateDisplayRecords();
        }
        catch
        {
            // If repositories are not available, collections will remain empty
            // This allows the UI to work even before ABP is fully initialized
        }
    }
}
