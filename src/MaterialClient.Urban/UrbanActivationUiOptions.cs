namespace MaterialClient.Urban;

/// <summary>
///     Urban 激活界面开关（离线 license.urban 流程保留实现，仅控制 UI 可见性）。
/// </summary>
internal static class UrbanActivationUiOptions
{
    /// <summary>
    ///     是否在未授权页展示离线下发授权（license.urban）相关说明与机器码复制区。
    /// </summary>
    public const bool ShowOfflineActivationUi = false;
}
