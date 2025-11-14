using System;
using System.Threading.Tasks;
using MaterialClient.Common.Entities;

namespace MaterialClient.Common.Tests.Services;

/// <summary>
/// 测试服务接口
/// 仅用于测试中的数据持久化操作，业务代码中不使用
/// </summary>
public interface ITestService
{
    /// <summary>
    /// 创建测试用的授权信息
    /// </summary>
    Task<LicenseInfo> CreateLicenseInfoAsync(
        Guid? id = null,
        Guid? projectId = null,
        Guid? authToken = null,
        DateTime? authEndTime = null,
        string? machineCode = null);

    /// <summary>
    /// 创建测试用的用户会话
    /// </summary>
    Task<UserSession> CreateUserSessionAsync(
        Guid? id = null,
        Guid? projectId = null,
        long userId = 1,
        string username = "testuser",
        string trueName = "测试用户",
        Guid? clientId = null,
        string accessToken = "test-token",
        bool isAdmin = false,
        bool isCompany = false,
        int productType = 1,
        long fromProductId = 1,
        long productId = 1,
        string productName = "测试产品",
        int companyId = 1,
        string companyName = "测试公司",
        string apiUrl = "http://test.com",
        DateTime? authEndTime = null,
        DateTime? lastActivityTime = null);

    /// <summary>
    /// 更新用户会话的最后活动时间
    /// </summary>
    Task UpdateUserSessionLastActivityTimeAsync(Guid sessionId, DateTime lastActivityTime);

    /// <summary>
    /// 创建测试用的用户凭证
    /// </summary>
    Task<UserCredential> CreateUserCredentialAsync(
        Guid? id = null,
        Guid? projectId = null,
        string username = "testuser",
        string encryptedPassword = "encrypted-password");

    /// <summary>
    /// 创建测试用的物料
    /// </summary>
    Task<Material> CreateMaterialAsync(
        int? id = null,
        string name = "测试物料",
        string? brand = null,
        string? size = null,
        decimal? upperLimit = null,
        decimal? lowerLimit = null,
        string? basicUnit = null,
        string? code = null,
        int coId = 1,
        string? specifications = null,
        string? proId = null,
        string? unitName = null,
        decimal unitRate = 1);

    /// <summary>
    /// 创建测试用的物料单位
    /// </summary>
    Task<MaterialUnit> CreateMaterialUnitAsync(
        int? id = null,
        int materialId = 1,
        string unitName = "kg",
        decimal rate = 1,
        int? providerId = null,
        string? rateName = null);

    /// <summary>
    /// 创建测试用的供应商
    /// </summary>
    Task<Provider> CreateProviderAsync(
        int? id = null,
        int providerType = 1,
        string providerName = "测试供应商",
        string? contactName = null,
        string? contactPhone = null);

    /// <summary>
    /// 清除所有测试数据
    /// </summary>
    Task ClearAllTestDataAsync();
}

