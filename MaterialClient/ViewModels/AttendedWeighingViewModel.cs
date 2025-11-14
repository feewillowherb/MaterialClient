using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Services;
using ReactiveUI;
using Volo.Abp.Domain.Repositories;

namespace MaterialClient.ViewModels;

public class AttendedWeighingViewModel : ViewModelBase
{
    private readonly IRepository<WeighingRecord, long>? _weighingRecordRepository;
    private readonly IRepository<Waybill, long>? _waybillRepository;

    private ObservableCollection<WeighingRecord> _unmatchedWeighingRecords = new();
    private ObservableCollection<Waybill> _completedWaybills = new();
    private WeighingRecord? _selectedWeighingRecord;
    private Waybill? _selectedWaybill;
    private ObservableCollection<string> _vehiclePhotos = new();
    private string? _billPhotoPath;
    private DateTime _currentTime = DateTime.Now;
    private decimal _currentWeight = 0.00m;
    private bool _isReceiving = true;
    private bool _showAllRecords = true;
    private bool _showUnmatched = false;
    private bool _showCompleted = false;
    private ObservableCollection<object> _displayRecords = new();
    private object? _selectedRecord;
    private string? _currentEntryPhoto1;
    private string? _currentEntryPhoto2;
    private string? _currentEntryPhoto3;
    private string? _currentEntryPhoto4;
    private string? _entryPhoto1;
    private string? _entryPhoto2;
    private string? _entryPhoto3;
    private string? _entryPhoto4;
    private string? _exitPhoto1;
    private string? _exitPhoto2;
    private string? _exitPhoto3;
    private string? _exitPhoto4;
    private string? _materialInfo;
    private string? _offsetInfo;

    public ObservableCollection<WeighingRecord> UnmatchedWeighingRecords
    {
        get => _unmatchedWeighingRecords;
        set => this.RaiseAndSetIfChanged(ref _unmatchedWeighingRecords, value);
    }

    public ObservableCollection<Waybill> CompletedWaybills
    {
        get => _completedWaybills;
        set => this.RaiseAndSetIfChanged(ref _completedWaybills, value);
    }

    public WeighingRecord? SelectedWeighingRecord
    {
        get => _selectedWeighingRecord;
        set => this.RaiseAndSetIfChanged(ref _selectedWeighingRecord, value);
    }

    public Waybill? SelectedWaybill
    {
        get => _selectedWaybill;
        set => this.RaiseAndSetIfChanged(ref _selectedWaybill, value);
    }

    public ObservableCollection<string> VehiclePhotos
    {
        get => _vehiclePhotos;
        set => this.RaiseAndSetIfChanged(ref _vehiclePhotos, value);
    }

    public string? BillPhotoPath
    {
        get => _billPhotoPath;
        set => this.RaiseAndSetIfChanged(ref _billPhotoPath, value);
    }

    public DateTime CurrentTime
    {
        get => _currentTime;
        set => this.RaiseAndSetIfChanged(ref _currentTime, value);
    }

    public decimal CurrentWeight
    {
        get => _currentWeight;
        set => this.RaiseAndSetIfChanged(ref _currentWeight, value);
    }

    public bool IsReceiving
    {
        get => _isReceiving;
        set => this.RaiseAndSetIfChanged(ref _isReceiving, value);
    }

    public bool ShowAllRecords
    {
        get => _showAllRecords;
        set => this.RaiseAndSetIfChanged(ref _showAllRecords, value);
    }

    public bool ShowUnmatched
    {
        get => _showUnmatched;
        set => this.RaiseAndSetIfChanged(ref _showUnmatched, value);
    }

    public bool ShowCompleted
    {
        get => _showCompleted;
        set => this.RaiseAndSetIfChanged(ref _showCompleted, value);
    }

    public ObservableCollection<object> DisplayRecords
    {
        get => _displayRecords;
        set => this.RaiseAndSetIfChanged(ref _displayRecords, value);
    }

    public object? SelectedRecord
    {
        get => _selectedRecord;
        set => this.RaiseAndSetIfChanged(ref _selectedRecord, value);
    }

    public string? CurrentEntryPhoto1
    {
        get => _currentEntryPhoto1;
        set => this.RaiseAndSetIfChanged(ref _currentEntryPhoto1, value);
    }

    public string? CurrentEntryPhoto2
    {
        get => _currentEntryPhoto2;
        set => this.RaiseAndSetIfChanged(ref _currentEntryPhoto2, value);
    }

    public string? CurrentEntryPhoto3
    {
        get => _currentEntryPhoto3;
        set => this.RaiseAndSetIfChanged(ref _currentEntryPhoto3, value);
    }

    public string? CurrentEntryPhoto4
    {
        get => _currentEntryPhoto4;
        set => this.RaiseAndSetIfChanged(ref _currentEntryPhoto4, value);
    }

    public string? EntryPhoto1
    {
        get => _entryPhoto1;
        set => this.RaiseAndSetIfChanged(ref _entryPhoto1, value);
    }

    public string? EntryPhoto2
    {
        get => _entryPhoto2;
        set => this.RaiseAndSetIfChanged(ref _entryPhoto2, value);
    }

    public string? EntryPhoto3
    {
        get => _entryPhoto3;
        set => this.RaiseAndSetIfChanged(ref _entryPhoto3, value);
    }

    public string? EntryPhoto4
    {
        get => _entryPhoto4;
        set => this.RaiseAndSetIfChanged(ref _entryPhoto4, value);
    }

    public string? ExitPhoto1
    {
        get => _exitPhoto1;
        set => this.RaiseAndSetIfChanged(ref _exitPhoto1, value);
    }

    public string? ExitPhoto2
    {
        get => _exitPhoto2;
        set => this.RaiseAndSetIfChanged(ref _exitPhoto2, value);
    }

    public string? ExitPhoto3
    {
        get => _exitPhoto3;
        set => this.RaiseAndSetIfChanged(ref _exitPhoto3, value);
    }

    public string? ExitPhoto4
    {
        get => _exitPhoto4;
        set => this.RaiseAndSetIfChanged(ref _exitPhoto4, value);
    }

    public string? MaterialInfo
    {
        get => _materialInfo;
        set => this.RaiseAndSetIfChanged(ref _materialInfo, value);
    }

    public string? OffsetInfo
    {
        get => _offsetInfo;
        set => this.RaiseAndSetIfChanged(ref _offsetInfo, value);
    }

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

        RefreshCommand = ReactiveCommand.CreateFromTask(RefreshAsync);
        SetReceivingCommand = ReactiveCommand.Create(() => IsReceiving = true);
        SetSendingCommand = ReactiveCommand.Create(() => IsReceiving = false);
        ShowAllRecordsCommand = ReactiveCommand.Create(() => SetDisplayMode(0));
        ShowUnmatchedCommand = ReactiveCommand.Create(() => SetDisplayMode(1));
        ShowCompletedCommand = ReactiveCommand.Create(() => SetDisplayMode(2));
        SelectRecordCommand = ReactiveCommand.Create<object>(OnRecordSelected);
        TakeBillPhotoCommand = ReactiveCommand.Create(OnTakeBillPhoto);
        SaveCommand = ReactiveCommand.Create(OnSave);
        CloseCommand = ReactiveCommand.Create(OnClose);

        // Subscribe to SelectedWeighingRecord changes
        this.WhenAnyValue(x => x.SelectedWeighingRecord)
            .Subscribe(OnSelectedWeighingRecordChanged);

        // Subscribe to SelectedWaybill changes
        this.WhenAnyValue(x => x.SelectedWaybill)
            .Subscribe(OnSelectedWaybillChanged);

        // Subscribe to display mode changes to update IsWeighingRecordSelected and IsWaybillSelected
        this.WhenAnyValue(
                x => x.SelectedWeighingRecord,
                x => x.SelectedWaybill,
                (record, waybill) => (record, waybill))
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(IsWeighingRecordSelected));
                this.RaisePropertyChanged(nameof(IsWaybillSelected));
            });

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

    private void OnSelectedWeighingRecordChanged(WeighingRecord? value)
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
        this.RaisePropertyChanged(nameof(IsWeighingRecordSelected));
        this.RaisePropertyChanged(nameof(IsWaybillSelected));
    }

    private void OnSelectedWaybillChanged(Waybill? value)
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
        this.RaisePropertyChanged(nameof(IsWeighingRecordSelected));
        this.RaisePropertyChanged(nameof(IsWaybillSelected));
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
