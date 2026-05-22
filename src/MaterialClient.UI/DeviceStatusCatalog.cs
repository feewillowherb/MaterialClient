using MaterialClient.UI.Models;

namespace MaterialClient.UI;

/// <summary>
///     Canonical device names and status bar item construction shared by MaterialClient and Urban.
/// </summary>
public static class DeviceStatusCatalog
{
    public const string ScaleName = "地磅设备";
    public const string CameraName = "摄像头";
    public const string UsbCameraName = "高拍仪";
    public const string PrinterName = "打印机";
    public const string LprName = "车牌识别";
    public const string SoundDeviceName = "音频设备";

    /// <summary>
    ///     Builds status bar items: scale, Hikvision camera(s), and LPR are always shown;
    ///     document camera, printer, and sound appear only when enabled in settings.
    /// </summary>
    public static DeviceStatusItem[] BuildItems(
        DeviceStatusBarOptions visibility,
        bool isScaleOnline,
        bool isCameraOnline,
        bool isUsbCameraOnline,
        bool isPrinterOnline,
        bool isLprOnline,
        bool isSoundDeviceOnline = false)
    {
        var items = new List<DeviceStatusItem>
        {
            new(ScaleName, isScaleOnline),
            new(CameraName, isCameraOnline),
        };

        if (visibility.DocumentCameraEnabled)
            items.Add(new(UsbCameraName, isUsbCameraOnline));

        if (visibility.PrinterEnabled)
            items.Add(new(PrinterName, isPrinterOnline));

        items.Add(new(LprName, isLprOnline));

        if (visibility.SoundDeviceEnabled)
            items.Add(new(SoundDeviceName, isSoundDeviceOnline));

        return items.ToArray();
    }

    /// <summary>
    ///     Builds <see cref="DeviceStatusBarOptions" /> from persisted settings.
    /// </summary>
    public static DeviceStatusBarOptions FromSettings(
        bool documentCameraEnabled,
        bool printerEnabled,
        bool soundDeviceEnabled) =>
        new(documentCameraEnabled, printerEnabled, soundDeviceEnabled);
}
