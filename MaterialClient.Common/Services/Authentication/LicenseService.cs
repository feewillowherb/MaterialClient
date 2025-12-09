using System;
using System.Linq;
using System.Threading.Tasks;
using MaterialClient.Common.Api;
using MaterialClient.Common.Api.Dtos;
using MaterialClient.Common.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Json;
using Volo.Abp.Uow;

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
    /// 测试方法：验证授权码（不联网，返回固定有效的授权信息）
    /// </summary>
    /// <param name="authorizationCode">授权码（测试方法中不进行实际验证）</param>
    /// <returns>固定的有效授权信息</returns>
    Task<LicenseInfo> VerifyAuthorizationCodeTestAsync(string authorizationCode);

    /// <summary>
    /// 获取当前授权信息
    /// </summary>
    /// <returns>授权信息，如果不存在则返回 null</returns>
    Task<LicenseInfo?> GetCurrentLicenseAsync();

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

/// <summary>
/// 授权许可服务实现
/// </summary>
[AutoConstructor]
public partial class LicenseService : DomainService, ILicenseService
{
    private readonly IBasePlatformApi _basePlatformApi;
    private readonly IMachineCodeService _machineCodeService;
    private readonly IRepository<LicenseInfo, Guid> _licenseRepository;
    private readonly IConfiguration _configuration;
    private readonly IJsonSerializer _jsonSerializer;

    [UnitOfWork]
    public async Task<LicenseInfo> VerifyAuthorizationCodeAsync(string authorizationCode)
    {
        if (string.IsNullOrWhiteSpace(authorizationCode))
        {
            throw new BusinessException("AUTH:EMPTY_CODE", "授权码不能为空");
        }

        // Get product code from configuration
        var productCode = _configuration["BasePlatform:ProductCode"];
        if (string.IsNullOrWhiteSpace(productCode))
        {
            throw new BusinessException("AUTH:NO_PRODUCT_CODE", "产品代码未配置");
        }

        // Get machine code
        var machineCode = _machineCodeService.GetMachineCode();

        // Call base platform API to verify authorization code
        var request = new LicenseRequestDto
        {
            ProductCode = productCode,
            Code = authorizationCode
        };

        HttpResult<string> response;
        try
        {
            response = await _basePlatformApi.GetAuthClientLicenseAsync(request);
        }
        catch (Exception ex)
        {
            throw new BusinessException("AUTH:API_ERROR", "无法连接到授权服务器，请检查网络连接", innerException: ex);
        }

        // Check response
        if (!response.Success || string.IsNullOrEmpty(response.Data))
        {
            var errorMsg = response?.Msg ?? "未知错误";
            throw new BusinessException("AUTH:INVALID_CODE", $"授权码验证失败：{errorMsg}");
        }

        var licenseDto = _jsonSerializer.Deserialize<LicenseInfoDto>(response.Data);

        // Verify machine code matches (if provided by API)
        // if (!string.IsNullOrWhiteSpace(licenseDto.MachineCode) &&
        //     !string.Equals(licenseDto.MachineCode, machineCode, StringComparison.OrdinalIgnoreCase))
        // {
        //     throw new BusinessException("AUTH:MACHINE_MISMATCH", "授权码与当前机器不匹配");
        // }

        // Check if license already exists
        var existingLicense = await _licenseRepository.FirstOrDefaultAsync();

        if (existingLicense != null)
        {
            // Check if project ID changed
            if (existingLicense.ProjectId != licenseDto.Proid)
            {
                // Project ID changed - delete old license (cascade will delete related records)
                await _licenseRepository.DeleteAsync(existingLicense);
                existingLicense = null;
            }
        }

        LicenseInfo license;
        var authEndTime = DateTime.Parse(licenseDto.AuthEndTime);
        if (existingLicense == null)
        {
            // Create new license
            license = new LicenseInfo(
                Guid.NewGuid(),
                licenseDto.Proid,
                licenseDto.AuthToken,
                authEndTime,
                machineCode
            );
            await _licenseRepository.InsertAsync(license);
        }
        else
        {
            // Update existing license
            existingLicense.Update(licenseDto.AuthToken, authEndTime, machineCode);
            license = await _licenseRepository.UpdateAsync(existingLicense);
        }

        return license;
    }

    /// <summary>
    /// 测试方法：验证授权码（不联网，返回固定有效的授权信息）
    /// 仅用于测试阶段，总是返回一个有效期为1年的固定授权信息
    /// </summary>
    /// <param name="authorizationCode">授权码（测试方法中不进行实际验证）</param>
    /// <returns>固定的有效授权信息</returns>
    [UnitOfWork]
    public async Task<LicenseInfo> VerifyAuthorizationCodeTestAsync(string authorizationCode)
    {
        if (string.IsNullOrWhiteSpace(authorizationCode))
        {
            throw new BusinessException("AUTH:EMPTY_CODE", "授权码不能为空");
        }

        // 获取机器码
        var machineCode = _machineCodeService.GetMachineCode();

        // 创建固定的测试授权信息
        var testLicenseId = Guid.Parse("00000000-0000-0000-0000-000000000001"); // 固定的测试授权ID（主键）
        var testProjectId = Guid.Parse("00000000-0000-0000-0000-000000000001"); // 固定的测试项目ID
        var testAuthToken = Guid.Parse("11111111-1111-1111-1111-111111111111"); // 固定的测试令牌
        var testAuthEndTime = DateTime.Now.AddYears(1); // 有效期1年

        // 检查是否已存在授权信息
        var existingLicense = await _licenseRepository.FirstOrDefaultAsync();

        LicenseInfo license;

        // 如果存在授权信息但ID不是固定的测试ID，先删除它（会级联删除关联的会话和凭证）
        if (existingLicense != null && existingLicense.Id != testLicenseId)
        {
            await _licenseRepository.DeleteAsync(existingLicense);
            existingLicense = null;
        }

        if (existingLicense == null)
        {
            // 创建新的测试授权信息，使用固定的ID
            license = new LicenseInfo(
                testLicenseId, // 使用固定的ID，确保外键约束能够匹配
                testProjectId,
                testAuthToken,
                testAuthEndTime,
                machineCode
            );
            await _licenseRepository.InsertAsync(license);
        }
        else
        {
            // 更新现有授权信息为测试数据（ID已经是固定的testLicenseId）
            existingLicense.ProjectId = testProjectId;
            existingLicense.Update(testAuthToken, testAuthEndTime, machineCode);
            license = await _licenseRepository.UpdateAsync(existingLicense);
        }

        return license;
    }

    public async Task<LicenseInfo?> GetCurrentLicenseAsync()
    {
        return await _licenseRepository.FirstOrDefaultAsync();
    }

    [UnitOfWork]
    public async Task<bool> IsLicenseValidAsync()
    {
        var license = await GetCurrentLicenseAsync();
        if (license == null)
        {
            return false;
        }

        // Check if expired
        return !license.IsExpired;
    }

    [UnitOfWork]
    public async Task ClearLicenseAsync()
    {
        var license = await GetCurrentLicenseAsync();
        if (license != null)
        {
            await _licenseRepository.DeleteAsync(license);
        }
    }
}