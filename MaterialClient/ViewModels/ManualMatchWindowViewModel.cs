using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
    private readonly IRepository<Provider, int>? _providerRepository;

    /// <summary>
    /// 当前称重记录
    /// </summary>
    public WeighingRecord CurrentRecord => _currentRecord;

    /// <summary>
    /// 车牌号
    /// </summary>
    [Reactive]
    private string? _plateNumber;

    /// <summary>
    /// 供料单位
    /// </summary>
    [Reactive]
    private string? _providerName;

    /// <summary>
    /// 车辆重量
    /// </summary>
    [Reactive]
    private decimal _weight;

    /// <summary>
    /// 进场时间
    /// </summary>
    [Reactive]
    private DateTime _joinTime;

    /// <summary>
    /// 是否收料（true=收料，false=发料）
    /// </summary>
    [Reactive]
    private bool _isReceiving = true;

    /// <summary>
    /// 选中的收发料类型
    /// </summary>
    public DeliveryType SelectedDeliveryType => IsReceiving ? DeliveryType.Receiving : DeliveryType.Sending;

    /// <summary>
    /// 可匹配订单列表
    /// </summary>
    [Reactive]
    private ObservableCollection<CandidateRecordViewModel> _candidateRecords = new();

    /// <summary>
    /// 选中的匹配订单
    /// </summary>
    [Reactive]
    private CandidateRecordViewModel? _selectedCandidateRecord;

    /// <summary>
    /// 是否可以点击确定
    /// </summary>
    [Reactive]
    private bool _canConfirm;

    /// <summary>
    /// 进场照片列表
    /// </summary>
    [Reactive]
    private ObservableCollection<string> _entryPhotos = new();

    /// <summary>
    /// 运单照片
    /// </summary>
    [Reactive]
    private string? _ticketPhoto;

    /// <summary>
    /// 总记录数
    /// </summary>
    [Reactive]
    private int _totalCount;

    /// <summary>
    /// 当前页
    /// </summary>
    [Reactive]
    private int _currentPage = 1;

    /// <summary>
    /// 总页数
    /// </summary>
    [Reactive]
    private int _totalPages = 1;

    /// <summary>
    /// 是否正在加载
    /// </summary>
    [Reactive]
    private bool _isLoading;

    public ManualMatchWindowViewModel(WeighingRecord currentRecord, IServiceProvider serviceProvider)
        : base(serviceProvider.GetService<ILogger<ManualMatchWindowViewModel>>())
    {
        _currentRecord = currentRecord;
        _serviceProvider = serviceProvider;
        _weighingMatchingService = serviceProvider.GetRequiredService<IWeighingMatchingService>();
        _providerRepository = serviceProvider.GetService<IRepository<Provider, int>>();

        // 初始化当前记录信息
        PlateNumber = currentRecord.PlateNumber;
        Weight = currentRecord.TotalWeight;
        JoinTime = currentRecord.CreationTime;

        // 如果记录已有 DeliveryType，使用它；否则默认收料
        IsReceiving = currentRecord.DeliveryType == null || currentRecord.DeliveryType == DeliveryType.Receiving;

        this.WhenAnyValue(x => x.IsReceiving)
            .Subscribe(async _ =>
            {
                this.RaisePropertyChanged(nameof(SelectedDeliveryType));
                await LoadCandidateRecordsAsync();
            });

        this.WhenAnyValue(x => x.SelectedCandidateRecord)
            .Subscribe(value => CanConfirm = value != null);

        // 加载数据
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await LoadProviderNameAsync();
        await LoadPhotosAsync();
        await LoadCandidateRecordsAsync();
    }

    #region 命令

    /// <summary>
    /// 加载可匹配订单
    /// </summary>
    [ReactiveCommand]
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
            Logger?.LogError(ex, "加载候选记录失败");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 设置为收料
    /// </summary>
    [ReactiveCommand]
    private void SetReceiving()
    {
        IsReceiving = true;
    }

    /// <summary>
    /// 设置为发料
    /// </summary>
    [ReactiveCommand]
    private void SetSending()
    {
        IsReceiving = false;
    }

    /// <summary>
    /// 上一页
    /// </summary>
    [ReactiveCommand]
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
    [ReactiveCommand]
    private void NextPage()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
        }
    }

    #endregion

    #region 私有方法

    private async Task LoadProviderNameAsync()
    {
        try
        {
            if (_providerRepository == null || !_currentRecord.ProviderId.HasValue)
            {
                ProviderName = null;
                return;
            }

            var provider = await _providerRepository.GetAsync(_currentRecord.ProviderId.Value);
            ProviderName = provider?.ProviderName;
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "加载供应商名称失败，ProviderId={ProviderId}", _currentRecord.ProviderId);
            ProviderName = null;
        }
    }

    private async Task LoadPhotosAsync()
    {
        try
        {
            var attachmentService = _serviceProvider.GetService<IAttachmentService>();
            if (attachmentService == null) return;

            var attachmentsDict = await attachmentService.GetAttachmentsByWeighingRecordIdsAsync(new[] { _currentRecord.Id });

            EntryPhotos.Clear();
            TicketPhoto = null;

            if (attachmentsDict.TryGetValue(_currentRecord.Id, out var attachmentFiles))
            {
                foreach (var file in attachmentFiles)
                {
                    if (!string.IsNullOrEmpty(file.LocalPath))
                    {
                        if (file.AttachType == AttachType.EntryPhoto)
                        {
                            EntryPhotos.Add(file.LocalPath);
                        }
                        else if (file.AttachType == AttachType.TicketPhoto)
                        {
                            TicketPhoto = file.LocalPath;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "加载照片失败");
        }
    }

    #endregion
}

/// <summary>
/// 候选匹配记录 ViewModel
/// </summary>
public partial class CandidateRecordViewModel : ReactiveObject
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
    [Reactive]
    private string? _providerName;

    /// <summary>
    /// 车辆重量
    /// </summary>
    public decimal Weight => Record.TotalWeight;

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
