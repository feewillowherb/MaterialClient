using MaterialClient.Common.Models;

namespace MaterialClient.Common.Services;

public record XiaoshanUploadSettingsFormState(
    bool WeighbridgeEnabled,
    bool GateEnabled,
    bool ProductEnabled,
    int WbInOutIndex,
    int GateDeviceIndex,
    int GateSiteIndex,
    int ProductDeviceIndex,
    int ProductSiteIndex);

public record XiaoshanUploadPreservedStatics(
    string? DisplayName,
    string? Remark,
    string? BuildLicenseNo,
    string? AreaCode,
    string? SpaceName,
    string? WeighbridgeDataSource);

public record XiaoshanUploadFormApplyResult(
    XiaoshanUploadSettingsFormState Form,
    XiaoshanUploadPreservedStatics Preserved);

public record XiaoshanUploadFormDraftResult(
    bool Success,
    string? ErrorCode,
    XiaoshanUploadConfigDraft? Draft);
