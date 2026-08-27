using MaterialClient.Common.Models;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Urban.Services;

public interface IXiaoshanUploadFieldMappingService : ITransientDependency
{
    XiaoshanFieldMappingResult MapForMode(
        string mode,
        XiaoshanUploadSettingsEnvelope settings,
        XiaoshanUploadModeSettings modeSettings,
        XiaoshanWeighingContext context);
}

public class XiaoshanUploadFieldMappingService : IXiaoshanUploadFieldMappingService
{
    public XiaoshanFieldMappingResult MapForMode(
        string mode,
        XiaoshanUploadSettingsEnvelope settings,
        XiaoshanUploadModeSettings modeSettings,
        XiaoshanWeighingContext context)
    {
        var resolved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var skipped = new List<XiaoshanSkippedField>();

        TryResolveStatic(resolved, skipped, mode, "buildLicenseNo", settings.BuildLicenseNo, required: true,
            transform: v => XiaoshanBuildLicenseNo.ForMode(mode, v));
        TryResolveStatic(resolved, skipped, mode, "areaCode", settings.AreaCode,
            required: mode is XiaoshanUploadModeNames.Gate or XiaoshanUploadModeNames.Product);
        TryResolveStatic(resolved, skipped, mode, "spaceName", settings.SpaceName, required: false);

        TryResolveRecord(resolved, skipped, mode, "carNo", context.CarNo, required: true);
        TryResolveRecord(resolved, skipped, mode, "carNoColor", context.CarNoColor, required: true);
        TryResolveRecord(resolved, skipped, mode, "carType", context.CarType,
            required: mode is XiaoshanUploadModeNames.Gate or XiaoshanUploadModeNames.Product);
        TryResolveRecord(resolved, skipped, mode, "goodsWeight", context.GoodsWeight, required: false);
        TryResolveRecord(resolved, skipped, mode, "snapTime", FormatSnapTime(context.SnapTime), required: true);
        TryResolveRecord(resolved, skipped, mode, "snapImages", FormatSnapImages(context.SnapImages), required: true);

        if (string.Equals(mode, XiaoshanUploadModeNames.Weighbridge, StringComparison.OrdinalIgnoreCase))
        {
            TryResolveStatic(resolved, skipped, mode, "dataSource",
                XiaoshanUploadDefaults.WeighbridgeDataSource, required: true);
            TryResolveStatic(resolved, skipped, mode, "inOutType", modeSettings.InOutType, required: true);
        }
        else
        {
            TryResolveStatic(resolved, skipped, mode, "deviceID", modeSettings.DeviceId, required: true);
            TryResolveStatic(resolved, skipped, mode, "siteType", modeSettings.SiteType, required: true);
        }

        return new XiaoshanFieldMappingResult(mode, resolved, skipped);
    }

    private static void TryResolveStatic(
        Dictionary<string, string> resolved,
        List<XiaoshanSkippedField> skipped,
        string mode,
        string field,
        string? value,
        bool required,
        Func<string, string>? transform = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            skipped.Add(new XiaoshanSkippedField(
                field,
                mode,
                required ? "Required static value missing" : "No data source; skipped",
                "static"));
            return;
        }

        resolved[field] = transform is null ? value : transform(value);
    }

    private static void TryResolveRecord(
        Dictionary<string, string> resolved,
        List<XiaoshanSkippedField> skipped,
        string mode,
        string field,
        string? value,
        bool required)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            skipped.Add(new XiaoshanSkippedField(
                field,
                mode,
                required ? "Required record value missing" : "No data source; skipped",
                "record"));
            return;
        }

        resolved[field] = value;
    }

    private static string? FormatSnapTime(DateTime? snapTime) =>
        snapTime?.ToString("yyyy-MM-dd HH:mm:ss");

    private static string? FormatSnapImages(IReadOnlyList<string>? images) =>
        images is null || images.Count == 0 ? null : string.Join(',', images);
}
