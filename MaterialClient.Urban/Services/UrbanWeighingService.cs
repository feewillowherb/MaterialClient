using MaterialClient.Common.Entities.Enums;
using Microsoft.Extensions.Logging;

namespace MaterialClient.Urban.Services;

/// <summary>
///     城管称重协调服务
///     保证 WeighingMode = UrbanMode (201)、ProductCode = 5030 的配置协调
/// </summary>
public interface IUrbanWeighingService
{
    /// <summary>
    ///     获取 Urban 称重模式
    /// </summary>
    WeighingMode WeighingMode { get; }

    /// <summary>
    ///     获取 Urban 产品代码
    /// </summary>
    ProductCode ProductCode { get; }
}

/// <summary>
///     城管称重协调服务实现
/// </summary>
public class UrbanWeighingService : IUrbanWeighingService
{
    private readonly ILogger<UrbanWeighingService> _logger;

    public UrbanWeighingService(ILogger<UrbanWeighingService> logger)
    {
        _logger = logger;
        _logger.LogInformation("UrbanWeighingService 初始化: WeighingMode={Mode}, ProductCode={Code}",
            WeighingMode, ProductCode);
    }

    /// <inheritdoc />
    public WeighingMode WeighingMode => WeighingMode.UrbanMode;

    /// <inheritdoc />
    public ProductCode ProductCode => ProductCode.Urban;
}
