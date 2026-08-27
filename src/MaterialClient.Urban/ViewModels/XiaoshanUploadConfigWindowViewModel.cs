using System.Reactive;
using System.Threading.Tasks;
using MaterialClient.Urban.Dtos;
using MaterialClient.Urban.Models;
using MaterialClient.Urban.Services;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Urban.ViewModels;

public partial class XiaoshanUploadConfigWindowViewModel : ReactiveObject, ITransientDependency
{
    private readonly IXiaoshanUploadConfigClientService _configService;

    [Reactive] private string? _displayName;
    [Reactive] private string? _remark;
    [Reactive] private long _configVersion;
    [Reactive] private bool _weighbridgeEnabled = true;
    [Reactive] private bool _gateEnabled;
    [Reactive] private bool _productEnabled;
    [Reactive] private string? _wbInOutType;
    [Reactive] private string? _wbDataSource = "WEIGHBRIDGE_XIAOSHAN";
    [Reactive] private string? _gateDeviceId;
    [Reactive] private string? _gateSiteType;
    [Reactive] private string? _productDeviceId;
    [Reactive] private string? _productSiteType;
    [Reactive] private string? _staticBuildLicenseNo;
    [Reactive] private string? _staticAreaCode;
    [Reactive] private string? _staticSpaceName;
    [Reactive] private string _skipHints = string.Empty;
    [Reactive] private string _alignmentStatus = "Unknown";
    [Reactive] private string? _statusMessage;
    [Reactive] private bool _isBusy;
    [Reactive] private bool _showAdvancedJson;
    [Reactive] private string _modesJson = "{}";
    [Reactive] private string _settingsJson = "{}";

    public XiaoshanUploadConfigWindowViewModel(IXiaoshanUploadConfigClientService configService)
    {
        _configService = configService;
        RefreshCommand = ReactiveCommand.CreateFromTask(RefreshAsync, this.WhenAnyValue(x => x.IsBusy, b => !b));
        SaveCommand = ReactiveCommand.CreateFromTask(SaveAsync, this.WhenAnyValue(x => x.IsBusy, b => !b));
        this.WhenAnyValue(x => x.StaticSpaceName).Subscribe(_ => UpdateSkipHints());
    }

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }

    public async Task InitializeAsync()
    {
        var local = await _configService.GetLocalAlignedAsync();
        if (local is not null)
        {
            ApplyDto(local);
            AlignmentStatus = $"Local cache v{local.ConfigVersion} (may be stale)";
        }

        try
        {
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Refresh failed: {ex.Message}";
            AlignmentStatus = "Not aligned";
        }
    }

    private async Task RefreshAsync()
    {
        IsBusy = true;
        StatusMessage = null;
        try
        {
            var remote = await _configService.RefreshFromServerAsync();
            ApplyDto(remote);
            AlignmentStatus = $"Aligned with server (v{remote.ConfigVersion})";
            StatusMessage = "Refreshed from server.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveAsync()
    {
        IsBusy = true;
        StatusMessage = null;
        try
        {
            BuildJsonFromForm();
            var draft = new XiaoshanUploadConfigWriteDto
            {
                DisplayName = DisplayName,
                Remark = Remark,
                ModesJson = ModesJson,
                SettingsJson = SettingsJson,
                ExpectedConfigVersion = ConfigVersion,
                ClientProtocolVersion = XiaoshanUploadClientProtocolVersions.Structured
            };

            var result = await _configService.SaveDraftToServerAsync(draft);
            if (result.Success && result.Config is not null)
            {
                ApplyDto(result.Config);
                AlignmentStatus = $"Aligned with server (v{result.Config.ConfigVersion})";
                StatusMessage = "Saved. Server accepted and local cache aligned.";
            }
            else if (result.IsConflict && result.Config is not null)
            {
                ApplyDto(result.Config);
                AlignmentStatus = $"Conflict — server v{result.Config.ConfigVersion} applied";
                StatusMessage = $"Version conflict. Your changes were not saved. {result.Message}";
            }
            else
            {
                AlignmentStatus = "Draft / not aligned";
                StatusMessage = $"Save failed. Draft kept locally. {result.Message}";
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyDto(XiaoshanUploadConfigDto dto)
    {
        DisplayName = dto.DisplayName;
        Remark = dto.Remark;
        ConfigVersion = dto.ConfigVersion;
        ModesJson = dto.ModesJson;
        SettingsJson = dto.SettingsJson;

        var modes = XiaoshanUploadEnvelopeJson.ParseModes(dto.ModesJson);
        var settings = XiaoshanUploadEnvelopeJson.ParseSettings(dto.SettingsJson);

        WeighbridgeEnabled = modes.IsEnabled(XiaoshanUploadModeNames.Weighbridge);
        GateEnabled = modes.IsEnabled(XiaoshanUploadModeNames.Gate);
        ProductEnabled = modes.IsEnabled(XiaoshanUploadModeNames.Product);

        var wb = modes.GetSettings(XiaoshanUploadModeNames.Weighbridge);
        WbInOutType = wb.InOutType;
        WbDataSource = wb.DataSource ?? "WEIGHBRIDGE_XIAOSHAN";

        var gate = modes.GetSettings(XiaoshanUploadModeNames.Gate);
        GateDeviceId = gate.DeviceId;
        GateSiteType = gate.SiteType;

        var product = modes.GetSettings(XiaoshanUploadModeNames.Product);
        ProductDeviceId = product.DeviceId;
        ProductSiteType = product.SiteType;

        StaticBuildLicenseNo = settings.BuildLicenseNo;
        StaticAreaCode = settings.AreaCode;
        StaticSpaceName = settings.SpaceName;

        UpdateSkipHints();
    }

    private void BuildJsonFromForm()
    {
        var enabled = new List<string>();
        if (WeighbridgeEnabled) enabled.Add(XiaoshanUploadModeNames.Weighbridge);
        if (GateEnabled) enabled.Add(XiaoshanUploadModeNames.Gate);
        if (ProductEnabled) enabled.Add(XiaoshanUploadModeNames.Product);

        var modes = new XiaoshanUploadModesEnvelope
        {
            EnabledModes = enabled,
            ModeSettings = new Dictionary<string, XiaoshanUploadModeSettings>(StringComparer.OrdinalIgnoreCase)
            {
                [XiaoshanUploadModeNames.Weighbridge] = new()
                {
                    InOutType = WbInOutType,
                    DataSource = WbDataSource
                },
                [XiaoshanUploadModeNames.Gate] = new()
                {
                    DeviceId = GateDeviceId,
                    SiteType = GateSiteType
                },
                [XiaoshanUploadModeNames.Product] = new()
                {
                    DeviceId = ProductDeviceId,
                    SiteType = ProductSiteType
                }
            }
        };

        var settings = new XiaoshanUploadSettingsEnvelope
        {
            BuildLicenseNo = StaticBuildLicenseNo,
            AreaCode = StaticAreaCode,
            SpaceName = StaticSpaceName
        };

        ModesJson = XiaoshanUploadEnvelopeJson.SerializeModes(modes);
        SettingsJson = XiaoshanUploadEnvelopeJson.SerializeSettings(settings);
        UpdateSkipHints();
    }

    private void UpdateSkipHints()
    {
        var hints = new List<string>();
        if (string.IsNullOrWhiteSpace(StaticSpaceName))
        {
            hints.Add("spaceName: 无数据源，跳过");
        }

        SkipHints = hints.Count == 0 ? string.Empty : string.Join("; ", hints);
    }
}
