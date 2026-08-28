using MaterialClient.Common.Events;
using MaterialClient.Common.Services;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;

namespace MaterialClient.Urban.Services;

public class XiaoshanUploadConfigSaveRequestedEventHandler
    : ILocalEventHandler<XiaoshanUploadConfigSaveRequestedEventData>, ITransientDependency
{
    private readonly IXiaoshanUploadConfigClientFacade _facade;
    private readonly ILogger<XiaoshanUploadConfigSaveRequestedEventHandler> _logger;

    public XiaoshanUploadConfigSaveRequestedEventHandler(
        IXiaoshanUploadConfigClientFacade facade,
        ILogger<XiaoshanUploadConfigSaveRequestedEventHandler> logger)
    {
        _facade = facade;
        _logger = logger;
    }

    public async Task HandleEventAsync(XiaoshanUploadConfigSaveRequestedEventData eventData)
    {
        try
        {
            var push = await _facade.PushToServerAsync(new XiaoshanUploadConfigDraft(
                eventData.ModesJson,
                eventData.SettingsJson));

            if (push.Success && push.Config is not null)
            {
                eventData.Completion.TrySetResult(new XiaoshanUploadConfigSyncResult(
                    true,
                    null,
                    push.Config.ModesJson,
                    push.Config.SettingsJson,
                    push.Config.ConfigVersion));
                return;
            }

            XiaoshanUploadConfigSnapshot? server = push.Config;
            if (server is null)
            {
                try
                {
                    server = await _facade.GetFromServerAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Reload server Xiaoshan config after failed push also failed");
                }
            }

            eventData.Completion.TrySetResult(new XiaoshanUploadConfigSyncResult(
                false,
                push.Message ?? "Push failed; local edits discarded.",
                server?.ModesJson ?? "{}",
                server?.SettingsJson ?? "{}",
                server?.ConfigVersion ?? 0));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Xiaoshan upload config LocalEvent handler failed");
            eventData.Completion.TrySetResult(new XiaoshanUploadConfigSyncResult(
                false,
                ex.Message,
                "{}",
                "{}",
                0));
        }
    }
}
