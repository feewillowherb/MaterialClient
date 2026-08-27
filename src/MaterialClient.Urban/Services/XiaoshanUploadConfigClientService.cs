using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Urban;
using MaterialClient.Common.Services.Authentication;
using MaterialClient.Urban.Api;
using MaterialClient.Urban.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace MaterialClient.Urban.Services;

public interface IXiaoshanUploadConfigClientService : ITransientDependency
{
    Task<XiaoshanUploadConfigDto?> GetLocalAlignedAsync(CancellationToken cancellationToken = default);

    Task<XiaoshanUploadConfigDto> RefreshFromServerAsync(CancellationToken cancellationToken = default);

    Task<XiaoshanUploadConfigSaveResult> SaveDraftToServerAsync(
        XiaoshanUploadConfigWriteDto draft,
        CancellationToken cancellationToken = default);
}

public class XiaoshanUploadConfigClientService : IXiaoshanUploadConfigClientService
{
    private const string ClientSource = "Client";

    private readonly IUrbanManagementApi _api;
    private readonly IRepository<LicenseInfo, Guid> _licenseRepository;
    private readonly IRepository<XiaoshanUploadConfigCache, Guid> _cacheRepository;
    private readonly IMachineCodeService _machineCodeService;
    private readonly ILogger<XiaoshanUploadConfigClientService> _logger;

    public XiaoshanUploadConfigClientService(
        IUrbanManagementApi api,
        IRepository<LicenseInfo, Guid> licenseRepository,
        IRepository<XiaoshanUploadConfigCache, Guid> cacheRepository,
        IMachineCodeService machineCodeService,
        ILogger<XiaoshanUploadConfigClientService> logger)
    {
        _api = api;
        _licenseRepository = licenseRepository;
        _cacheRepository = cacheRepository;
        _machineCodeService = machineCodeService;
        _logger = logger;
    }

    public async Task<XiaoshanUploadConfigDto?> GetLocalAlignedAsync(CancellationToken cancellationToken = default)
    {
        var projectId = await GetProjectIdAsync(cancellationToken);
        if (projectId is null)
        {
            return null;
        }

        var query = await _cacheRepository.GetQueryableAsync();
        var cache = await query.FirstOrDefaultAsync(c => c.ProjectId == projectId.Value, cancellationToken);
        return cache is null ? null : ToDto(cache);
    }

    [UnitOfWork]
    public virtual async Task<XiaoshanUploadConfigDto> RefreshFromServerAsync(CancellationToken cancellationToken = default)
    {
        var projectId = await RequireProjectIdAsync(cancellationToken);
        var remote = await _api.GetXiaoshanUploadConfigAsync(projectId);
        await UpsertAlignedCacheAsync(remote, cancellationToken);
        return remote;
    }

    [UnitOfWork]
    public virtual async Task<XiaoshanUploadConfigSaveResult> SaveDraftToServerAsync(
        XiaoshanUploadConfigWriteDto draft,
        CancellationToken cancellationToken = default)
    {
        var projectId = await RequireProjectIdAsync(cancellationToken);
        draft.ProjectId = projectId;
        draft.Source = ClientSource;
        draft.Actor = _machineCodeService.GetMachineCode();

        var query = await _cacheRepository.GetQueryableAsync();
        var cache = await query.FirstOrDefaultAsync(c => c.ProjectId == projectId, cancellationToken);
        draft.ExpectedConfigVersion = cache?.ConfigVersion ?? 0;

        try
        {
            var result = await _api.WriteXiaoshanUploadConfigAsync(draft);

            if (result.IsConflict && result.Config is not null)
            {
                await UpsertAlignedCacheAsync(result.Config, cancellationToken);
                return new XiaoshanUploadConfigSaveResult(
                    Success: false,
                    IsAlignedWithServer: true,
                    IsConflict: true,
                    Message: result.Message ?? "Config version conflict; server snapshot applied.",
                    Config: result.Config);
            }

            if (result.Success && result.Config is not null)
            {
                await UpsertAlignedCacheAsync(result.Config, cancellationToken);
                return new XiaoshanUploadConfigSaveResult(
                    Success: true,
                    IsAlignedWithServer: true,
                    IsConflict: false,
                    Message: null,
                    Config: result.Config);
            }

            await UpsertDraftCacheAsync(draft, aligned: false, cancellationToken);
            return new XiaoshanUploadConfigSaveResult(
                Success: false,
                IsAlignedWithServer: false,
                IsConflict: false,
                Message: result.Message ?? "Write rejected by server.",
                Config: result.Config);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Xiaoshan upload config write-back failed for ProjectId={ProjectId}", projectId);
            await UpsertDraftCacheAsync(draft, aligned: false, cancellationToken);
            return new XiaoshanUploadConfigSaveResult(
                Success: false,
                IsAlignedWithServer: false,
                IsConflict: false,
                Message: ex.Message,
                Config: null);
        }
    }

    private async Task UpsertAlignedCacheAsync(XiaoshanUploadConfigDto remote, CancellationToken cancellationToken)
    {
        var query = await _cacheRepository.GetQueryableAsync();
        var cache = await query.FirstOrDefaultAsync(c => c.ProjectId == remote.ProjectId, cancellationToken);
        if (cache is null)
        {
            cache = new XiaoshanUploadConfigCache(Guid.NewGuid(), remote.ProjectId);
            ApplyRemote(cache, remote, aligned: true);
            await _cacheRepository.InsertAsync(cache, autoSave: true, cancellationToken: cancellationToken);
        }
        else
        {
            ApplyRemote(cache, remote, aligned: true);
            await _cacheRepository.UpdateAsync(cache, autoSave: true, cancellationToken: cancellationToken);
        }
    }

    private async Task UpsertDraftCacheAsync(
        XiaoshanUploadConfigWriteDto draft,
        bool aligned,
        CancellationToken cancellationToken)
    {
        var query = await _cacheRepository.GetQueryableAsync();
        var cache = await query.FirstOrDefaultAsync(c => c.ProjectId == draft.ProjectId, cancellationToken);
        if (cache is null)
        {
            cache = new XiaoshanUploadConfigCache(Guid.NewGuid(), draft.ProjectId);
            ApplyDraft(cache, draft, aligned);
            await _cacheRepository.InsertAsync(cache, autoSave: true, cancellationToken: cancellationToken);
        }
        else
        {
            ApplyDraft(cache, draft, aligned);
            await _cacheRepository.UpdateAsync(cache, autoSave: true, cancellationToken: cancellationToken);
        }
    }

    private static void ApplyRemote(XiaoshanUploadConfigCache cache, XiaoshanUploadConfigDto remote, bool aligned)
    {
        cache.ServerConfigId = remote.Id;
        cache.DisplayName = remote.DisplayName;
        cache.Remark = remote.Remark;
        cache.ModesJson = string.IsNullOrWhiteSpace(remote.ModesJson) ? "{}" : remote.ModesJson;
        cache.SettingsJson = string.IsNullOrWhiteSpace(remote.SettingsJson) ? "{}" : remote.SettingsJson;
        cache.ConfigVersion = remote.ConfigVersion;
        cache.IsAlignedWithServer = aligned;
    }

    private static void ApplyDraft(XiaoshanUploadConfigCache cache, XiaoshanUploadConfigWriteDto draft, bool aligned)
    {
        cache.DisplayName = draft.DisplayName;
        cache.Remark = draft.Remark;
        cache.ModesJson = string.IsNullOrWhiteSpace(draft.ModesJson) ? "{}" : draft.ModesJson!;
        cache.SettingsJson = string.IsNullOrWhiteSpace(draft.SettingsJson) ? "{}" : draft.SettingsJson!;
        cache.IsAlignedWithServer = aligned;
    }

    private static XiaoshanUploadConfigDto ToDto(XiaoshanUploadConfigCache cache) => new()
    {
        Id = cache.ServerConfigId,
        ProjectId = cache.ProjectId,
        DisplayName = cache.DisplayName,
        Remark = cache.Remark,
        ModesJson = cache.ModesJson,
        SettingsJson = cache.SettingsJson,
        ConfigVersion = cache.ConfigVersion
    };

    private async Task<Guid?> GetProjectIdAsync(CancellationToken cancellationToken)
    {
        var query = await _licenseRepository.GetQueryableAsync();
        var license = await query.FirstOrDefaultAsync(cancellationToken);
        return license?.ProjectId;
    }

    private async Task<Guid> RequireProjectIdAsync(CancellationToken cancellationToken)
    {
        var projectId = await GetProjectIdAsync(cancellationToken);
        if (projectId is null || projectId == Guid.Empty)
        {
            throw new InvalidOperationException("LicenseInfo ProjectId is missing.");
        }

        return projectId.Value;
    }
}
