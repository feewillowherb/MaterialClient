using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Events;

namespace MaterialClient.Common.Services.GateIO;

/// <summary>
///     道闸 IO 控制器接口
///     使用 GateIODirection 而非 LicensePlateDirection，实现领域分离
/// </summary>
public interface IGateIOController
{
    /// <summary>
    ///     验证配置是否有效
    /// </summary>
    Task<GateIOConfigurationValidationResult> ValidateConfigurationAsync(LicensePlateRecognitionConfig config);

    /// <summary>
    ///     打开指定方向的道闸
    /// </summary>
    Task OpenGateAsync(GateIODirection direction, int durationMs = 500);

    /// <summary>
    ///     关闭指定方向的道闸
    /// </summary>
    Task CloseGateAsync(GateIODirection direction);

    /// <summary>
    ///     向指定方向的道闸写入输出值
    /// </summary>
    Task WriteOutputAsync(GateIODirection direction, bool value);
}
