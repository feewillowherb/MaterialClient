namespace MaterialClient.Common.Events;

/// <summary>
///     道闸 IO 配置验证结果
/// </summary>
public class GateIOConfigurationValidationResult
{
    /// <summary>
    ///     配置是否有效
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    ///     验证错误消息列表
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    ///     创建成功的验证结果
    /// </summary>
    public static GateIOConfigurationValidationResult Success()
    {
        return new GateIOConfigurationValidationResult { IsValid = true };
    }

    /// <summary>
    ///     创建失败的验证结果
    /// </summary>
    public static GateIOConfigurationValidationResult Failed(params string[] errors)
    {
        return new GateIOConfigurationValidationResult
        {
            IsValid = false,
            Errors = errors.ToList()
        };
    }
}
