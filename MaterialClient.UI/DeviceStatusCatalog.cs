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
    ///     Builds the standard status bar items matching MaterialClient main app ordering.
    /// </summary>
    public static DeviceStatusItem[] BuildItems(
        bool isScaleOnline,
        bool isCameraOnline,
        bool isUsbCameraOnline,
        bool isPrinterOnline,
        bool isLprOnline,
        bool? isSoundDeviceOnline = null)
    {
        var items = new List<DeviceStatusItem>
        {
            new(ScaleName, isScaleOnline),
            new(CameraName, isCameraOnline),
            new(UsbCameraName, isUsbCameraOnline),
            new(PrinterName, isPrinterOnline),
            new(LprName, isLprOnline),
        };

        if (isSoundDeviceOnline.HasValue)
        {
            items.Add(new(SoundDeviceName, isSoundDeviceOnline.Value));
        }

        return items.ToArray();
    }
}
