using MaterialClient.Common.Entities;
using MaterialClient.Common.Services;
using MaterialClient.Common.Services.Authentication;
using MaterialClient.Urban.Api;
using MaterialClient.Urban.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace MaterialClient.Urban.Services;

/// <summary>
///     Server-authoritative Xiaoshan upload config client (no local cache table).
/// </summary>
/// <remarks>
///     Interface name ends with Facade (not Service), so ABP conventional registration
///     would not expose it; ExposeServices is required for DI consumers.
/// </remarks>
[ExposeServices(typeof(IXiaoshanUploadConfigClientFacade))]
public class XiaoshanUploadConfigClientService : IXiaoshanUploadConfigClientFacade, ITransientDependency
{
    private const string ClientSource = "Client";

    private readonly IUrbanManagementApi _api;
    private readonly IRepository<LicenseInfo, Guid> _licenseRepository;
    private readonly IMachineCodeService _machineCodeService;
    private readonly ILogger<XiaoshanUploadConfigClientService> _logger;

    public XiaoshanUploadConfigClientService(
        IUrbanManagementApi api,
        IRepository<LicenseInfo, Guid> licenseRepository,
        IMachineCodeService machineCodeService,
        ILogger<XiaoshanUploadConfigClientService> logger)
    {
        _api = api;
        _licenseRepository = licenseRepository;
        _machineCodeService = machineCodeService;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task<XiaoshanUploadConfigSnapshot> GetFromServerAsync(
        CancellationToken cancellationToken = default)
    {
        var projectId = await RequireProjectIdAsync(cancellationToken);
        var remote = await _api.GetXiaoshanUploadConfigAsync(projectId);
        return ToSnapshot(remote);
    }

    [UnitOfWork]
    public virtual async Task<XiaoshanUploadConfigSyncPushResult> PushToServerAsync(
        XiaoshanUploadConfigDraft draft,
        CancellationToken cancellationToken = default)
    {
        var projectId = await RequireProjectIdAsync(cancellationToken);

        long expectedVersion = 0;
        try
        {
            var current = await _api.GetXiaoshanUploadConfigAsync(projectId);
            expectedVersion = current.ConfigVersion;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read current Xiaoshan config before push for ProjectId={ProjectId}", projectId);
        }

        var writeDto = new XiaoshanUploadConfigWriteDto
        {
            ProjectId = projectId,
            DisplayName = draft.DisplayName,
            Remark = draft.Remark,
            ModesJson = draft.ModesJson,
            SettingsJson = draft.SettingsJson,
            ExpectedConfigVersion = expectedVersion,
            Source = ClientSource,
            Actor = _machineCodeService.GetMachineCode(),
            ClientProtocolVersion = XiaoshanUploadClientProtocolVersions.Structured
        };

        try
        {
            var result = await _api.WriteXiaoshanUploadConfigAsync(writeDto);

            if (result.Success && result.Config is not null)
            {
                _logger.LogInformation(
                    "Xiaoshan upload config push succeeded for ProjectId={ProjectId}",
                    projectId);
                return new XiaoshanUploadConfigSyncPushResult(true, null, ToSnapshot(result.Config));
            }

            _logger.LogWarning(
                "Xiaoshan upload config push rejected for ProjectId={ProjectId}: {Message}",
                projectId,
                result.Message);

            var server = await TryGetSnapshotAsync(projectId, result.Config);
            return new XiaoshanUploadConfigSyncPushResult(
                false,
                result.Message ?? "Write rejected by server.",
                server);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Xiaoshan upload config push failed for ProjectId={ProjectId}", projectId);
            var server = await TryGetSnapshotAsync(projectId, config: null);
            return new XiaoshanUploadConfigSyncPushResult(false, ex.Message, server);
        }
    }

    private async Task<XiaoshanUploadConfigSnapshot?> TryGetSnapshotAsync(
        Guid projectId,
        XiaoshanUploadConfigDto? config)
    {
        if (config is not null)
        {
            return ToSnapshot(config);
        }

        try
        {
            var remote = await _api.GetXiaoshanUploadConfigAsync(projectId);
            return ToSnapshot(remote);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to reload Xiaoshan config after push failure ProjectId={ProjectId}", projectId);
            return null;
        }
    }

    private static XiaoshanUploadConfigSnapshot ToSnapshot(XiaoshanUploadConfigDto dto) =>
        new(
            dto.DisplayName,
            dto.Remark,
            string.IsNullOrWhiteSpace(dto.ModesJson) ? "{}" : dto.ModesJson,
            string.IsNullOrWhiteSpace(dto.SettingsJson) ? "{}" : dto.SettingsJson,
            dto.ConfigVersion);

    private async Task<Guid> RequireProjectIdAsync(CancellationToken cancellationToken)
    {
        var query = await _licenseRepository.GetQueryableAsync();
        var license = await query.FirstOrDefaultAsync(cancellationToken);
        if (license?.ProjectId is null || license.ProjectId == Guid.Empty)
        {
            throw new InvalidOperationException("LicenseInfo ProjectId is missing.");
        }

        return license.ProjectId;
    }
}
