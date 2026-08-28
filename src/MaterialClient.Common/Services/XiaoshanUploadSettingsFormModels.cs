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

public record XiaoshanUploadModesPersistResult(
    bool Success,
    string ModesJson);
