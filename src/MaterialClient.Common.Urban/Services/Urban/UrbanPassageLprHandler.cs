using MaterialClient.Common.Configuration;
using MaterialClient.Common.Dtos.Urban;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Events;
using MaterialClient.Common.Services;
using MaterialClient.Common.Services.Urban;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Uow;

namespace MaterialClient.Common.Urban.Services.Urban;

public class UrbanPassageLprHandler :
    ILocalEventHandler<LicensePlateRecognizedEventData>,
    ITransientDependency
{
    private readonly ISettingsService _settingsService;
    private readonly IUrbanPassageRecordService _passageRecordService;
    private readonly IRepository<AttachmentFile, int> _attachmentFileRepository;
    private readonly ILocalEventBus _localEventBus;
    private readonly ILogger<UrbanPassageLprHandler> _logger;

    public UrbanPassageLprHandler(
        ISettingsService settingsService,
        IUrbanPassageRecordService passageRecordService,
        IRepository<AttachmentFile, int> attachmentFileRepository,
        ILocalEventBus localEventBus,
        ILogger<UrbanPassageLprHandler> logger)
    {
        _settingsService = settingsService;
        _passageRecordService = passageRecordService;
        _attachmentFileRepository = attachmentFileRepository;
        _localEventBus = localEventBus;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(LicensePlateRecognizedEventData eventData)
    {
        var settings = await _settingsService.GetSettingsAsync();
        var config = LicensePlateRecognitionConfig.FindByDeviceName(
            settings.LicensePlateRecognitionConfigs,
            eventData.DeviceName);
        if (config is null || config.SiteType == LprSiteType.Scale)
            return;

        var source = config.SiteType == LprSiteType.FinishedProduct
            ? PassageSource.FinishedProduct
            : PassageSource.Checkpoint;

        int? largeId = null;
        if (!string.IsNullOrWhiteSpace(eventData.LprImagePath))
        {
            var file = new AttachmentFile(
                Path.GetFileName(eventData.LprImagePath),
                eventData.LprImagePath,
                AttachType.Lpr);
            await _attachmentFileRepository.InsertAsync(file, autoSave: true);
            largeId = file.Id;
        }

        var context = new UrbanLprCaptureContext(
            eventData.PlateNumber,
            eventData.PlateColor,
            eventData.VehicleType,
            eventData.Timestamp == default ? DateTime.Now : eventData.Timestamp,
            source,
            config.UrbanInOutType,
            config.UrbanSiteType,
            largeId,
            null);

        var created = await _passageRecordService.CreateFromLprAsync(context);
        await _localEventBus.PublishAsync(new UrbanPassageRecordCreatedEventData { PassageRecordId = created.Id });
        _logger.LogInformation(
            "Created urban passage record from {Device} site {SiteType}",
            eventData.DeviceName,
            config.SiteType);
    }
}
