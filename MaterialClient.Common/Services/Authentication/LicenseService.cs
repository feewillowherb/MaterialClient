using System;
using System.Linq;
using System.Threading.Tasks;
using MaterialClient.Common.Api;
using MaterialClient.Common.Api.Dtos;
using MaterialClient.Common.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace MaterialClient.Common.Services.Authentication;

/// <summary>
/// 授权许可服务实现
/// </summary>
public class LicenseService : ILicenseService, ITransientDependency
{
    private readonly IBasePlatformApi _basePlatformApi;
    private readonly IMachineCodeService _machineCodeService;
    private readonly IRepository<LicenseInfo, Guid> _licenseRepository;
    private readonly IConfiguration _configuration;

    public LicenseService(
        IBasePlatformApi basePlatformApi,
        IMachineCodeService machineCodeService,
        IRepository<LicenseInfo, Guid> licenseRepository,
        IConfiguration configuration)
    {
        _basePlatformApi = basePlatformApi;
        _machineCodeService = machineCodeService;
        _licenseRepository = licenseRepository;
        _configuration = configuration;
    }

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
        var machineCode = await _machineCodeService.GetMachineCodeAsync();

        // Call base platform API to verify authorization code
        var request = new LicenseRequestDto
        {
            ProductCode = productCode,
            Code = authorizationCode
        };

        HttpResult<LicenseInfoDto> response;
        try
        {
            response = await _basePlatformApi.GetAuthClientLicenseAsync(request);
        }
        catch (Exception ex)
        {
            throw new BusinessException("AUTH:API_ERROR", "无法连接到授权服务器，请检查网络连接", innerException: ex);
        }

        // Check response
        if (response == null || !response.Success || response.Data == null)
        {
            var errorMsg = response?.Msg ?? "未知错误";
            throw new BusinessException("AUTH:INVALID_CODE", $"授权码验证失败：{errorMsg}");
        }

        var licenseDto = response.Data;

        // Verify machine code matches (if provided by API)
        if (!string.IsNullOrWhiteSpace(licenseDto.MachineCode) && 
            !string.Equals(licenseDto.MachineCode, machineCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("AUTH:MACHINE_MISMATCH", "授权码与当前机器不匹配");
        }

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
        if (existingLicense == null)
        {
            // Create new license
            license = new LicenseInfo(
                Guid.NewGuid(),
                licenseDto.Proid,
                licenseDto.AuthToken,
                licenseDto.AuthEndTime,
                machineCode
            );
            await _licenseRepository.InsertAsync(license);
        }
        else
        {
            // Update existing license
            existingLicense.Update(licenseDto.AuthToken, licenseDto.AuthEndTime, machineCode);
            license = await _licenseRepository.UpdateAsync(existingLicense);
        }

        return license;
    }

    public async Task<LicenseInfo> GetCurrentLicenseAsync()
    {
        return await _licenseRepository.FirstOrDefaultAsync();
    }

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

    public async Task ClearLicenseAsync()
    {
        var license = await GetCurrentLicenseAsync();
        if (license != null)
        {
            await _licenseRepository.DeleteAsync(license);
        }
    }
}

