using System.Reactive;
using System.Threading.Tasks;
using MaterialClient.Urban.Dtos;
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
    [Reactive] private string _modesJson = "{}";
    [Reactive] private string _settingsJson = "{}";
    [Reactive] private string _alignmentStatus = "Unknown";
    [Reactive] private string? _statusMessage;
    [Reactive] private bool _isBusy;

    public XiaoshanUploadConfigWindowViewModel(IXiaoshanUploadConfigClientService configService)
    {
        _configService = configService;
        RefreshCommand = ReactiveCommand.CreateFromTask(RefreshAsync, this.WhenAnyValue(x => x.IsBusy, b => !b));
        SaveCommand = ReactiveCommand.CreateFromTask(SaveAsync, this.WhenAnyValue(x => x.IsBusy, b => !b));
    }

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }

    public async Task InitializeAsync()
    {
        var local = await _configService.GetLocalAlignedAsync();
        if (local is not null)
        {
            ApplyDto(local);
            AlignmentStatus = "Local cache (may be stale)";
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
            AlignmentStatus = "Aligned with server";
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
            var draft = new XiaoshanUploadConfigWriteDto
            {
                DisplayName = DisplayName,
                Remark = Remark,
                ModesJson = ModesJson,
                SettingsJson = SettingsJson
            };

            var result = await _configService.SaveDraftToServerAsync(draft);
            if (result.Success && result.Config is not null)
            {
                ApplyDto(result.Config);
                AlignmentStatus = "Aligned with server";
                StatusMessage = "Saved. Server accepted and local cache aligned.";
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
        ModesJson = string.IsNullOrWhiteSpace(dto.ModesJson) ? "{}" : dto.ModesJson;
        SettingsJson = string.IsNullOrWhiteSpace(dto.SettingsJson) ? "{}" : dto.SettingsJson;
    }
}
