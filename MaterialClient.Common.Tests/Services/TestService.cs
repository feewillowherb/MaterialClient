using System;
using System.Linq;
using System.Threading.Tasks;
using MaterialClient.Common.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MaterialClient.Common.Tests.Services;

/// <summary>
/// 测试服务实现
/// 仅用于测试中的数据持久化操作，业务代码中不使用
/// </summary>
public class TestService : DomainService, ITestService
{
    private readonly IRepository<LicenseInfo, Guid> _licenseRepository;
    private readonly IRepository<UserSession, Guid> _sessionRepository;
    private readonly IRepository<UserCredential, Guid> _credentialRepository;
    private readonly IRepository<Material, int> _materialRepository;
    private readonly IRepository<MaterialUnit, int> _materialUnitRepository;
    private readonly IRepository<Provider, int> _providerRepository;

    public TestService(
        IRepository<LicenseInfo, Guid> licenseRepository,
        IRepository<UserSession, Guid> sessionRepository,
        IRepository<UserCredential, Guid> credentialRepository,
        IRepository<Material, int> materialRepository,
        IRepository<MaterialUnit, int> materialUnitRepository,
        IRepository<Provider, int> providerRepository)
    {
        _licenseRepository = licenseRepository;
        _sessionRepository = sessionRepository;
        _credentialRepository = credentialRepository;
        _materialRepository = materialRepository;
        _materialUnitRepository = materialUnitRepository;
        _providerRepository = providerRepository;
    }

    public async Task<LicenseInfo> CreateLicenseInfoAsync(
        Guid? id = null,
        Guid? projectId = null,
        Guid? authToken = null,
        DateTime? authEndTime = null,
        string? machineCode = null)
    {
        var license = new LicenseInfo(
            id ?? Guid.NewGuid(),
            projectId ?? Guid.NewGuid(),
            authToken ?? Guid.NewGuid(),
            authEndTime ?? DateTime.UtcNow.AddMonths(6),
            machineCode ?? "test-machine-code"
        );

        return await _licenseRepository.InsertAsync(license);
    }

    public async Task<UserSession> CreateUserSessionAsync(
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
        DateTime? lastActivityTime = null)
    {
        // Get projectId from license if not provided
        if (projectId == null)
        {
            var license = await _licenseRepository.FirstOrDefaultAsync();
            if (license != null)
            {
                projectId = license.ProjectId;
            }
            else
            {
                projectId = Guid.NewGuid();
            }
        }

        var session = new UserSession(
            id ?? Guid.NewGuid(),
            projectId.Value,
            userId,
            username,
            trueName,
            clientId ?? Guid.NewGuid(),
            accessToken,
            isAdmin,
            isCompany,
            productType,
            fromProductId,
            productId,
            productName,
            companyId,
            companyName,
            apiUrl,
            authEndTime
        );

        if (lastActivityTime.HasValue)
        {
            session.LastActivityTime = lastActivityTime.Value;
        }

        return await _sessionRepository.InsertAsync(session);
    }

    public async Task UpdateUserSessionLastActivityTimeAsync(Guid sessionId, DateTime lastActivityTime)
    {
        var session = await _sessionRepository.GetAsync(sessionId);
        session.LastActivityTime = lastActivityTime;
        await _sessionRepository.UpdateAsync(session);
    }

    public async Task<UserCredential> CreateUserCredentialAsync(
        Guid? id = null,
        Guid? projectId = null,
        string username = "testuser",
        string encryptedPassword = "encrypted-password")
    {
        // Get projectId from license if not provided
        if (projectId == null)
        {
            var license = await _licenseRepository.FirstOrDefaultAsync();
            if (license != null)
            {
                projectId = license.ProjectId;
            }
            else
            {
                projectId = Guid.NewGuid();
            }
        }

        var credential = new UserCredential(
            id ?? Guid.NewGuid(),
            projectId.Value,
            username,
            encryptedPassword
        );

        return await _credentialRepository.InsertAsync(credential);
    }

    public async Task<Material> CreateMaterialAsync(
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
        decimal unitRate = 1)
    {
        var materialId = id ?? 0; // Will be auto-generated if 0
        var material = new Material(materialId, name, coId)
        {
            Brand = brand,
            Size = size,
            UpperLimit = upperLimit,
            LowerLimit = lowerLimit,
            BasicUnit = basicUnit,
            Code = code,
            Specifications = specifications,
            ProId = proId,
            UnitName = unitName,
            UnitRate = unitRate
        };

        return await _materialRepository.InsertAsync(material);
    }

    public async Task<MaterialUnit> CreateMaterialUnitAsync(
        int? id = null,
        int materialId = 1,
        string unitName = "kg",
        decimal rate = 1,
        int? providerId = null,
        string? rateName = null)
    {
        var materialUnitId = id ?? 0; // Will be auto-generated if 0
        var materialUnit = new MaterialUnit(materialUnitId, materialId, unitName, rate)
        {
            ProviderId = providerId,
            RateName = rateName
        };

        return await _materialUnitRepository.InsertAsync(materialUnit);
    }

    public async Task<Provider> CreateProviderAsync(
        int? id = null,
        int providerType = 1,
        string providerName = "测试供应商",
        string? contactName = null,
        string? contactPhone = null)
    {
        var providerId = id ?? 0; // Will be auto-generated if 0
        var provider = new Provider(providerId, providerType, providerName)
        {
            ContactName = contactName,
            ContactPhone = contactPhone
        };

        return await _providerRepository.InsertAsync(provider);
    }

    public async Task ClearAllTestDataAsync()
    {
        var sessions = await _sessionRepository.GetListAsync();
        foreach (var session in sessions)
        {
            await _sessionRepository.DeleteAsync(session);
        }

        var credentials = await _credentialRepository.GetListAsync();
        foreach (var credential in credentials)
        {
            await _credentialRepository.DeleteAsync(credential);
        }

        var licenses = await _licenseRepository.GetListAsync();
        foreach (var license in licenses)
        {
            await _licenseRepository.DeleteAsync(license);
        }

        var materials = await _materialRepository.GetListAsync();
        foreach (var material in materials)
        {
            await _materialRepository.DeleteAsync(material);
        }

        var materialUnits = await _materialUnitRepository.GetListAsync();
        foreach (var materialUnit in materialUnits)
        {
            await _materialUnitRepository.DeleteAsync(materialUnit);
        }

        var providers = await _providerRepository.GetListAsync();
        foreach (var provider in providers)
        {
            await _providerRepository.DeleteAsync(provider);
        }
    }
}

