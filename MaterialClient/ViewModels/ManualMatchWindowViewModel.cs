using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Volo.Abp.Domain.Repositories;

namespace MaterialClient.ViewModels;

/// <summary>
/// 手动匹配窗口 ViewModel
/// </summary>
public partial class ManualMatchWindowViewModel : ViewModelBase
{
    private readonly WeighingRecord _currentRecord;
    private readonly IServiceProvider _serviceProvider;
    private readonly IWeighingMatchingService _weighingMatchingService;
    private readonly IRepository<WeighingRecordAttachment, int>? _attachmentRepository;

    #region 属性

    /// <summary>
    /// 当前称重记录
    /// </summary>
    public WeighingRecord CurrentRecord => _currentRecord;

    /// <summary>
    /// 车牌号
    /// </summary>
    [ObservableProperty] private string? _plateNumber;

    /// <summary>
    /// 供料单位
    /// </summary>
    [ObservableProperty] private string? _providerName;

    /// <summary>
    /// 车辆重量
    /// </summary>
    [ObservableProperty] private decimal _weight;

    /// <summary>
    /// 进场时间
    /// </summary>
    [ObservableProperty] private DateTime _joinTime;

    /// <summary>
    /// 是否收料（true=收料，false=发料）
    /// </summary>
    [ObservableProperty] private bool _isReceiving = true;

    /// <summary>
    /// 选中的收发料类型
    /// </summary>
    public DeliveryType SelectedDeliveryType => IsReceiving ? DeliveryType.Receiving : DeliveryType.Sending;

    /// <summary>
    /// 可匹配订单列表
    /// </summary>
    [ObservableProperty] private ObservableCollection<CandidateRecordViewModel> _candidateRecords = new();

    /// <summary>
    /// 选中的匹配订单
    /// </summary>
    [ObservableProperty] private CandidateRecordViewModel? _selectedCandidateRecord;

    /// <summary>
    /// 是否可以点击确定
    /// </summary>
    [ObservableProperty] private bool _canConfirm;

    /// <summary>
    /// 进场照片列表
    /// </summary>
    [ObservableProperty] private ObservableCollection<string> _entryPhotos = new();

    /// <summary>
    /// 运单照片
    /// </summary>
    [ObservableProperty] private string? _ticketPhoto;

    /// <summary>
    /// 总记录数
    /// </summary>
    [ObservableProperty] private int _totalCount;

    /// <summary>
    /// 当前页
    /// </summary>
    [ObservableProperty] private int _currentPage = 1;

    /// <summary>
    /// 总页数
    /// </summary>
    [ObservableProperty] private int _totalPages = 1;

    /// <summary>
    /// 是否正在加载
    /// </summary>
    [ObservableProperty] private bool _isLoading;

    #endregion

    public ManualMatchWindowViewModel(WeighingRecord currentRecord, IServiceProvider serviceProvider)
    {
        _currentRecord = currentRecord;
        _serviceProvider = serviceProvider;
        _weighingMatchingService = serviceProvider.GetRequiredService<IWeighingMatchingService>();
        _attachmentRepository = serviceProvider.GetService<IRepository<WeighingRecordAttachment, int>>();

        // 初始化当前记录信息
        PlateNumber = currentRecord.PlateNumber;
        Weight = currentRecord.Weight;
        JoinTime = currentRecord.CreationTime;

        // 如果记录已有 DeliveryType，使用它；否则默认收料
        IsReceiving = currentRecord.DeliveryType == null || currentRecord.DeliveryType == DeliveryType.Receiving;

        // 监听选中项变化
        this.WhenAnyValue(x => x.SelectedCandidateRecord)
            .Subscribe(_ => UpdateCanConfirm());

        // 监听收发料类型变化，重新加载候选记录
        this.WhenAnyValue(x => x.IsReceiving)
            .Subscribe(async _ => await LoadCandidateRecordsAsync());

        // 加载数据
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await LoadPhotosAsync();
        await LoadCandidateRecordsAsync();
    }

    private void UpdateCanConfirm()
    {
        CanConfirm = SelectedCandidateRecord != null;
    }

    #region 命令

    /// <summary>
    /// 加载可匹配订单
    /// </summary>
    [RelayCommand]
    private async Task LoadCandidateRecordsAsync()
    {
        try
        {
            IsLoading = true;
            CandidateRecords.Clear();

            var candidates = await _weighingMatchingService.GetCandidateRecordsAsync(
                _currentRecord,
                SelectedDeliveryType);

            foreach (var record in candidates)
            {
                CandidateRecords.Add(new CandidateRecordViewModel(record, _currentRecord.CreationTime));
            }

            TotalCount = CandidateRecords.Count;
            TotalPages = Math.Max(1, (int)Math.Ceiling(TotalCount / 10.0));
            CurrentPage = 1;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载候选记录失败: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 设置为收料
    /// </summary>
    [RelayCommand]
    private void SetReceiving()
    {
        IsReceiving = true;
    }

    /// <summary>
    /// 设置为发料
    /// </summary>
    [RelayCommand]
    private void SetSending()
    {
        IsReceiving = false;
    }

    /// <summary>
    /// 上一页
    /// </summary>
    [RelayCommand]
    private void PreviousPage()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
        }
    }

    /// <summary>
    /// 下一页
    /// </summary>
    [RelayCommand]
    private void NextPage()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
        }
    }

    #endregion

    #region 私有方法

    private async Task LoadPhotosAsync()
    {
        try
        {
            if (_attachmentRepository == null) return;

            var attachments = await _attachmentRepository.GetListAsync(
                predicate: x => x.WeighingRecordId == _currentRecord.Id,
                includeDetails: true
            );

            EntryPhotos.Clear();
            TicketPhoto = null;

            foreach (var attachment in attachments)
            {
                if (attachment.AttachmentFile != null && !string.IsNullOrEmpty(attachment.AttachmentFile.LocalPath))
                {
                    if (attachment.AttachmentFile.AttachType == AttachType.EntryPhoto)
                    {
                        EntryPhotos.Add(attachment.AttachmentFile.LocalPath);
                    }
                    else if (attachment.AttachmentFile.AttachType == AttachType.TicketPhoto)
                    {
                        TicketPhoto = attachment.AttachmentFile.LocalPath;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载照片失败: {ex.Message}");
        }
    }

    #endregion

    #region 属性变更

    partial void OnIsReceivingChanged(bool value)
    {
        OnPropertyChanged(nameof(SelectedDeliveryType));
    }

    #endregion
}

/// <summary>
/// 候选匹配记录 ViewModel
/// </summary>
public partial class CandidateRecordViewModel : ObservableObject
{
    /// <summary>
    /// 原始称重记录
    /// </summary>
    public WeighingRecord Record { get; }

    /// <summary>
    /// 车牌号
    /// </summary>
    public string? PlateNumber => Record.PlateNumber;

    /// <summary>
    /// 供料单位
    /// </summary>
    [ObservableProperty] private string? _providerName;

    /// <summary>
    /// 车辆重量
    /// </summary>
    public decimal Weight => Record.Weight;

    /// <summary>
    /// 进场时间
    /// </summary>
    public DateTime JoinTime => Record.CreationTime;

    /// <summary>
    /// 相隔时间（与当前记录的时间差）
    /// </summary>
    public string SeparatedTime { get; }

    public CandidateRecordViewModel(WeighingRecord record, DateTime currentRecordTime)
    {
        Record = record;

        // 计算时间差
        var diff = record.CreationTime - currentRecordTime;
        if (diff.TotalDays >= 1)
        {
            SeparatedTime = $"{(int)diff.TotalDays}天{diff.Hours}时";
        }
        else if (diff.TotalHours >= 1)
        {
            SeparatedTime = $"{(int)diff.TotalHours}时{diff.Minutes}分";
        }
        else
        {
            SeparatedTime = $"{(int)diff.TotalMinutes}分钟";
        }
    }
}