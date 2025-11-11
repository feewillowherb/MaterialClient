using System;
using System.Threading.Tasks;
using MaterialClient.Common.Entities;

namespace MaterialClient.Common.Services.Authentication;

/// <summary>
/// 授权许可服务接口
/// 负责软件授权验证和授权信息管理
/// </summary>
public interface ILicenseService
{
    /// <summary>
    /// 验证授权码并保存授权信息
    /// </summary>
    /// <param name="authorizationCode">授权码</param>
    /// <returns>授权信息</returns>
    /// <exception cref="Volo.Abp.BusinessException">授权码无效或验证失败</exception>
    Task<LicenseInfo> VerifyAuthorizationCodeAsync(string authorizationCode);

    /// <summary>
    /// 获取当前授权信息
    /// </summary>
    /// <returns>授权信息，如果不存在则返回 null</returns>
    Task<LicenseInfo> GetCurrentLicenseAsync();

    /// <summary>
    /// 检查授权是否有效（存在且未过期）
    /// </summary>
    /// <returns>true 表示授权有效，false 表示授权无效或不存在</returns>
    Task<bool> IsLicenseValidAsync();

    /// <summary>
    /// 删除当前授权信息（用于项目ID变更时）
    /// </summary>
    Task ClearLicenseAsync();
}

