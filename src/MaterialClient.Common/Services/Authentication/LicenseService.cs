using MaterialClient.Common.Api;
using MaterialClient.Common.Api.Dtos;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Services;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Json;
using Volo.Abp.Uow;

namespace MaterialClient.Common.Services.Authentication;

public interface ILicenseService
{
    Task<LicenseInfo> VerifyAuthorizationCodeAsync(string authorizationCode, ProductCode productCode);

    Task<LicenseInfo> VerifyAuthorizationCodeTestAsync(string authorizationCode);

    Task<LicenseInfo?> GetCurrentLicenseAsync();

    Task<bool> IsLicenseValidAsync();

    Task ClearLicenseAsync();

    Task<bool> SyncProjectFieldsFromServerAsync(
        string? proName,
        string? accessCode,
        DateTime? authEndTime);

    Task StoreServerJwtAsync(
        string serverJwt,
        string proName,
        string? accessCode,
        DateTime authEndTime);

    Task<string?> GetLocalJwtTokenAsync(string licenseFilePath);

    Task<LicenseInfo> ActivateUrbanAsync(string accessCode);
}

[AutoConstructor]
public partial class LicenseService : DomainService, ILicenseService
{
    private readonly IBasePlatformApi _basePlatformApi;
    private readonly IJsonSerializer _jsonSerializer;
    private readonly IRepository<LicenseInfo, Guid> _licenseRepository;
    private readonly IMachineCodeService _machineCodeService;
    private readonly IStaticLicenseChecker _staticLicenseChecker;

    [UnitOfWork]
    public async Task<LicenseInfo> VerifyAuthorizationCodeAsync(string authorizationCode, ProductCode productCode)
    {
        if (productCode == ProductCode.Urban)
        {
            throw new BusinessException(
                "AUTH:URBAN_DIRECT_ACTIVATION_FORBIDDEN",
                "城管产品请使用 Urban 在线激活，不可直连 BasePlatform");
        }

        if (string.IsNullOrWhiteSpace(authorizationCode))
        {
            throw new BusinessException("AUTH:EMPTY_CODE", "授权码不能为空");
        }

        var machineCode = _machineCodeService.GetMachineCode();

        var request = new LicenseRequestDto
        {
            ProductCode = ((int)productCode).ToString(),
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

        if (!response.Success || string.IsNullOrEmpty(response.Data))
        {
            var errorMsg = response?.Msg ?? "未知错误";
            throw new BusinessException("AUTH:INVALID_CODE", $"授权码验证失败：{errorMsg}");
        }

        var licenseDto = _jsonSerializer.Deserialize<LicenseInfoDto>(response.Data);

        var existingLicense = await _licenseRepository.FirstOrDefaultAsync();

        if (existingLicense != null && existingLicense.ProjectId != licenseDto.Proid)
        {
            await _licenseRepository.DeleteAsync(existingLicense);
            existingLicense = null;
        }

        LicenseInfo license;
        if (existingLicense == null)
        {
            license = new LicenseInfo(
                Guid.NewGuid(),
                licenseDto.Proid,
                licenseDto.AuthEndTime,
                machineCode);
            await _licenseRepository.InsertAsync(license);
        }
        else
        {
            existingLicense.Update(licenseDto.AuthEndTime, machineCode);
            license = await _licenseRepository.UpdateAsync(existingLicense);
        }

        return license;
    }

    [UnitOfWork]
    public async Task<LicenseInfo> VerifyAuthorizationCodeTestAsync(string authorizationCode)
    {
        if (string.IsNullOrWhiteSpace(authorizationCode))
        {
            throw new BusinessException("AUTH:EMPTY_CODE", "授权码不能为空");
        }

        var machineCode = _machineCodeService.GetMachineCode();

        var testLicenseId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var testProjectId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var testAuthEndTime = DateTime.Now.AddYears(1);

        var existingLicense = await _licenseRepository.FirstOrDefaultAsync();

        LicenseInfo license;

        if (existingLicense != null && existingLicense.Id != testLicenseId)
        {
            await _licenseRepository.DeleteAsync(existingLicense);
            existingLicense = null;
        }

        if (existingLicense == null)
        {
            license = new LicenseInfo(
                testLicenseId,
                testProjectId,
                testAuthEndTime,
                machineCode);
            await _licenseRepository.InsertAsync(license);
        }
        else
        {
            existingLicense.ProjectId = testProjectId;
            existingLicense.Update(testAuthEndTime, machineCode);
            license = await _licenseRepository.UpdateAsync(existingLicense);
        }

        return license;
    }

    [UnitOfWork]
    public async Task<LicenseInfo?> GetCurrentLicenseAsync()
        => await _licenseRepository.FirstOrDefaultAsync();

    [UnitOfWork]
    public async Task<bool> IsLicenseValidAsync()
    {
        var license = await GetCurrentLicenseAsync();
        return license is { IsExpired: false };
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

    [UnitOfWork]
    public async Task<bool> SyncProjectFieldsFromServerAsync(
        string? proName,
        string? accessCode,
        DateTime? authEndTime)
    {
        var license = await GetCurrentLicenseAsync();
        if (license == null)
        {
            return false;
        }

        var updated = false;

        if (!string.IsNullOrWhiteSpace(proName) &&
            !string.Equals(license.ProName, proName, StringComparison.Ordinal))
        {
            license.ProName = proName;
            updated = true;
        }

        if (!string.IsNullOrWhiteSpace(accessCode) &&
            !string.Equals(license.AccessCode, accessCode, StringComparison.Ordinal))
        {
            license.AccessCode = accessCode;
            updated = true;
        }

        if (authEndTime.HasValue && license.AuthEndTime != authEndTime.Value)
        {
            license.AuthEndTime = authEndTime.Value;
            updated = true;
        }

        if (!updated)
        {
            return false;
        }

        license.UpdatedAt = DateTime.Now;
        await _licenseRepository.UpdateAsync(license);
        return true;
    }

    [UnitOfWork]
    public async Task StoreServerJwtAsync(
        string serverJwt,
        string proName,
        string? accessCode,
        DateTime authEndTime)
    {
        var license = await GetCurrentLicenseAsync();
        if (license == null)
        {
            return;
        }

        license.LatestJwtToken = serverJwt;
        license.ProName = proName;
        license.AccessCode = accessCode;
        license.AuthEndTime = authEndTime;
        license.UpdatedAt = DateTime.Now;
        await _licenseRepository.UpdateAsync(license);
    }

    [UnitOfWork]
    public async Task<string?> GetLocalJwtTokenAsync(string licenseFilePath)
    {
        var license = await GetCurrentLicenseAsync();
        if (license != null && !string.IsNullOrWhiteSpace(license.LatestJwtToken))
        {
            return license.LatestJwtToken;
        }

        if (File.Exists(licenseFilePath))
        {
            try
            {
                var content = await File.ReadAllTextAsync(licenseFilePath);
                if (!string.IsNullOrWhiteSpace(content))
                {
                    return content.Trim();
                }
            }
            catch
            {
                // Ignore file read errors
            }
        }

        return null;
    }

    [UnitOfWork]
    public async Task<LicenseInfo> ActivateUrbanAsync(string accessCode)
    {
        if (string.IsNullOrWhiteSpace(accessCode))
        {
            throw new BusinessException("AUTH:EMPTY_CODE", "授权码不能为空");
        }

        var urbanAuthApi = LazyServiceProvider.LazyGetService<IUrbanAuthApi>();
        if (urbanAuthApi == null)
        {
            throw new BusinessException("AUTH:URBAN_API_UNAVAILABLE", "Urban 授权 API 未注册");
        }

        var machineCode = _machineCodeService.GetMachineCode();
        var request = new ActivateUrbanRequest((int)ProductCode.Urban, accessCode.Trim(), machineCode);

        HttpResult<ActivateUrbanResponseData> response;
        try
        {
            response = await urbanAuthApi.ActivateUrbanAsync(request);
        }
        catch (Exception ex)
        {
            throw new BusinessException("AUTH:API_ERROR", "无法连接到 Urban 授权服务", innerException: ex);
        }

        if (!response.Success || response.Data == null || string.IsNullOrWhiteSpace(response.Data.JwtToken))
        {
            throw new BusinessException("AUTH:ACTIVATE_FAILED", response.Msg ?? "在线激活失败");
        }

        var checkResult = await _staticLicenseChecker.CheckLicenseFromTokenAsync(response.Data.JwtToken);
        if (!checkResult.IsSuccess || checkResult.ProId == Guid.Empty)
        {
            throw new BusinessException("AUTH:JWT_INVALID", checkResult.Message);
        }

        var existingLicense = await _licenseRepository.FirstOrDefaultAsync();
        if (existingLicense != null && existingLicense.ProjectId != checkResult.ProId)
        {
            await _licenseRepository.DeleteAsync(existingLicense);
            existingLicense = null;
        }

        LicenseInfo license;
        if (existingLicense == null)
        {
            license = new LicenseInfo(
                Guid.NewGuid(),
                checkResult.ProId,
                checkResult.AuthEndTime,
                machineCode,
                checkResult.ProName,
                checkResult.AccessCode)
            {
                LatestJwtToken = response.Data.JwtToken.Trim()
            };
            await _licenseRepository.InsertAsync(license);
        }
        else
        {
            existingLicense.ProjectId = checkResult.ProId;
            existingLicense.LatestJwtToken = response.Data.JwtToken.Trim();
            existingLicense.Update(
                checkResult.AuthEndTime,
                machineCode,
                checkResult.ProName,
                checkResult.AccessCode);
            license = await _licenseRepository.UpdateAsync(existingLicense);
        }

        return license;
    }
}
