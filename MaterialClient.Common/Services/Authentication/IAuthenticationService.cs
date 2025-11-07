using System.Threading.Tasks;
using MaterialClient.Common.Entities;

namespace MaterialClient.Common.Services.Authentication;

/// <summary>
/// 用户认证服务接口
/// 负责用户登录、会话管理和凭证管理
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// 用户登录
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="password">密码（明文）</param>
    /// <param name="rememberMe">是否记住密码</param>
    /// <returns>用户会话信息</returns>
    /// <exception cref="Volo.Abp.BusinessException">登录失败</exception>
    Task<UserSession> LoginAsync(string username, string password, bool rememberMe);

    /// <summary>
    /// 获取当前用户会话
    /// </summary>
    /// <returns>用户会话信息，如果不存在则返回 null</returns>
    Task<UserSession> GetCurrentSessionAsync();

    /// <summary>
    /// 检查是否有活跃的会话
    /// </summary>
    /// <returns>true 表示有活跃会话，false 表示无会话或会话已过期</returns>
    Task<bool> HasActiveSessionAsync();

    /// <summary>
    /// 登出当前用户
    /// </summary>
    Task LogoutAsync();

    /// <summary>
    /// 获取保存的用户凭证（用于自动登录）
    /// </summary>
    /// <returns>用户名和密码（明文），如果不存在则返回 null</returns>
    Task<(string username, string password)?> GetSavedCredentialAsync();

    /// <summary>
    /// 清除保存的用户凭证
    /// </summary>
    Task ClearSavedCredentialAsync();

    /// <summary>
    /// 更新会话活动时间
    /// </summary>
    Task UpdateSessionActivityAsync();
}

