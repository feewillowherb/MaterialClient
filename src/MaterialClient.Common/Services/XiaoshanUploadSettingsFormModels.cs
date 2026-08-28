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

public record XiaoshanUploadFormDraftResult(
    bool Success,
    string? ErrorCode,
    XiaoshanUploadConfigDraft? Draft);
