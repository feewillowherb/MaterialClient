using System.Text.Json.Serialization;
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
    ///     车牌识别设备类型 (Hikvision / Vzvision / Huaxiazhixin)
    ///     默认值: Hikvision
    /// </summary>
    [JsonConverter(typeof(LprDeviceTypeJsonConverter))]
    public LprDeviceType LprDeviceType { get; set; } = LprDeviceType.Hikvision;

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
    ///     Enable document camera (高拍仪 / USB camera) functionality and status bar indicator
    /// </summary>
    public bool DocumentCameraEnabled { get; set; } = false;

    /// <summary>
    ///     Enable printer functionality
    /// </summary>
    public bool EnablePrinter { get; set; } = false;

    /// <summary>
    ///     Selected printer name for printing tickets
    /// </summary>
    public string SelectedPrinterName { get; set; } = string.Empty;

    /// <summary>
    ///     Default export directory path (remembered after successful export)
    /// </summary>
    public string ExportDefaultPath { get; set; } = string.Empty;

    /// <summary>
    ///     启用最新推荐数据（使用缓存数据而非数据库查询）
    /// </summary>
    public bool EnableLatestRecommendation { get; set; } = false;

    /// <summary>
    ///     启用 LPR 主动抓拍功能（通用总开关，默认关闭）
    /// </summary>
    public bool EnableTriggerLprCapture { get; set; } = false;

    /// <summary>
    ///     JPEG 抓拍压缩质量（1-100），默认 75。
    ///     值 >= 100 时跳过压缩，保留原始文件。
    /// </summary>
    public int JpegQuality { get; set; } = 75;

    // ========== Urban 配置 ==========

    /// <summary>
    ///     Urban 模式标识
    /// </summary>
    public bool IsUrbanMode { get; set; } = false;

    /// <summary>
    ///     Urban 产品代码（5030）
    /// </summary>
    public int UrbanProductCode { get; set; } = 5030;

    /// <summary>
    ///     静态授权文件路径
    /// </summary>
    public string LicenseFilePath { get; set; } = "license.urban";

    /// <summary>
    ///     Urban 异常检测阈值配置（单位：吨）。
    /// </summary>
    public UrbanAnomalyDetectionConfig UrbanAnomalyDetection { get; set; } = new();
}