using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Common.Configuration;

/// <summary>
///     System settings configuration
/// </summary>
public class SystemSettings
{
    /// <summary>
    ///     Enable auto-start on boot
    /// </summary>
    public bool EnableAutoStart { get; set; } = false;

    /// <summary>
    ///     Capture stream type (Substream or Mainstream)
    /// </summary>
    public StreamType CaptureStreamType { get; set; } = StreamType.Substream;

    /// <summary>
    ///     Web service URLs
    /// </summary>
    public string Urls { get; set; } = "http://localhost:9960";

    /// <summary>
    ///     Snapshot camera type (Hikvision or LPRAllInOne)
    ///     Default: Hikvision
    /// </summary>
    public SnapshotCameraType SnapshotCameraType { get; set; } = SnapshotCameraType.Hikvision;

    /// <summary>
    ///     最小字符差异数（0-2），用于车牌号推荐匹配
    /// </summary>
    public int MinDiffCharCount { get; set; } = 0;

    /// <summary>
    ///     默认称重模式（Standard 或 SolidWaste）
    ///     用于系统级别的默认模式设置
    /// </summary>
    public WeighingMode DefaultWeighingMode { get; set; } = WeighingMode.Standard;

    /// <summary>
    ///     Enable printer functionality
    /// </summary>
    public bool EnablePrinter { get; set; } = false;

    /// <summary>
    ///     Selected printer name for printing tickets
    /// </summary>
    public string SelectedPrinterName { get; set; } = string.Empty;
}