using System.Threading;
using MaterialClient.Common.Api;
using MaterialClient.Common.Api.Dtos;
using MaterialClient.Common.EntityFrameworkCore;
using MaterialClient.Common.Services.Authentication;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MaterialClient.Common.Tests;

/// <summary>
/// 授权服务集成测试
/// This test will be implemented after LicenseService is created in Phase 3.2
/// </summary>
public class LicenseServiceIntegrationTests : MaterialClientEntityFrameworkCoreTestBase
{
    private readonly IBasePlatformApi _mockBasePlatformApi;
    private readonly IMachineCodeService _machineCodeService;
    // private readonly ILicenseService _licenseService;
    // private readonly IRepository<LicenseInfo, Guid> _licenseRepository;

    public LicenseServiceIntegrationTests()
    {
        _mockBasePlatformApi = Substitute.For<IBasePlatformApi>();
        _machineCodeService = GetRequiredService<IMachineCodeService>();

        // Replace real API with mock
        ServiceProvider.GetRequiredService<IBasePlatformApi>().Returns(_mockBasePlatformApi);

        // _licenseService = GetRequiredService<ILicenseService>();
        // _licenseRepository = GetRequiredService<IRepository<LicenseInfo, Guid>>();
    }

    [Fact(Skip = "Will be implemented after LicenseService is created")]
    public async Task VerifyAuthorizationCode_ValidCode_ShouldSaveLicenseInfo()
    {
        // Arrange
        var authCode = "test-auth-code-123";
        var expectedLicenseDto = new LicenseInfoDto
        {
            Proid = Guid.NewGuid(),
            AuthToken = Guid.NewGuid(),
            AuthEndTime = DateTime.Now.AddMonths(1),
            MachineCode = _machineCodeService.GetMachineCode()
        };

        _mockBasePlatformApi.GetAuthClientLicenseAsync(Arg.Any<LicenseRequestDto>(), Arg.Any<CancellationToken>())
            .Returns(new HttpResult<LicenseInfoDto>
            {
                Code = 200,
                Success = true,
                Data = expectedLicenseDto,
                Msg = "Success"
            });

        // Act
        // var result = await _licenseService.VerifyAuthorizationCodeAsync(authCode);

        // Assert
        // result.ShouldNotBeNull();
        // result.ProjectId.ShouldBe(expectedLicenseDto.Proid);
        // result.IsExpired.ShouldBeFalse();

        // Verify saved to database
        // var savedLicense = await _licenseRepository.FirstOrDefaultAsync();
        // savedLicense.ShouldNotBeNull();
        // savedLicense.ProjectId.ShouldBe(expectedLicenseDto.Proid);
    }

    [Fact(Skip = "Will be implemented after LicenseService is created")]
    public async Task VerifyAuthorizationCode_InvalidCode_ShouldThrowException()
    {
        // Arrange
        var authCode = "invalid-code";

        _mockBasePlatformApi.GetAuthClientLicenseAsync(Arg.Any<LicenseRequestDto>(), Arg.Any<CancellationToken>())
            .Returns(new HttpResult<LicenseInfoDto>
            {
                Code = 400,
                Success = false,
                Data = null,
                Msg = "Invalid authorization code"
            });

        // Act & Assert
        // await Should.ThrowAsync<BusinessException>(async () =>
        //     await _licenseService.VerifyAuthorizationCodeAsync(authCode)
        // );
    }

    [Fact(Skip = "Will be implemented after LicenseService is created")]
    public async Task GetCurrentLicense_NoLicense_ShouldReturnNull()
    {
        // Act
        // var license = await _licenseService.GetCurrentLicenseAsync();

        // Assert
        // license.ShouldBeNull();
    }

    [Fact(Skip = "Will be implemented after LicenseService is created")]
    public async Task GetCurrentLicense_ValidLicense_ShouldReturnLicense()
    {
        // Arrange - create a valid license first
        var authCode = "test-auth-code-123";
        var expectedLicenseDto = new LicenseInfoDto
        {
            Proid = Guid.NewGuid(),
            AuthToken = Guid.NewGuid(),
            AuthEndTime = DateTime.Now.AddMonths(1),
            MachineCode = _machineCodeService.GetMachineCode()
        };

        _mockBasePlatformApi.GetAuthClientLicenseAsync(Arg.Any<LicenseRequestDto>(), Arg.Any<CancellationToken>())
            .Returns(new HttpResult<LicenseInfoDto>
            {
                Code = 200,
                Success = true,
                Data = expectedLicenseDto,
                Msg = "Success"
            });

        // await _licenseService.VerifyAuthorizationCodeAsync(authCode);

        // Act
        // var license = await _licenseService.GetCurrentLicenseAsync();

        // Assert
        // license.ShouldNotBeNull();
        // license.ProjectId.ShouldBe(expectedLicenseDto.Proid);
    }

    [Fact(Skip = "Will be implemented after LicenseService is created")]
    public async Task IsLicenseValid_ExpiredLicense_ShouldReturnFalse()
    {
        // Arrange - create an expired license
        var authCode = "test-auth-code-expired";
        var expiredLicenseDto = new LicenseInfoDto
        {
            Proid = Guid.NewGuid(),
            AuthToken = Guid.NewGuid(),
            AuthEndTime = DateTime.Now.AddDays(-1), // Expired yesterday
            MachineCode = _machineCodeService.GetMachineCode()
        };

        _mockBasePlatformApi.GetAuthClientLicenseAsync(Arg.Any<LicenseRequestDto>(), Arg.Any<CancellationToken>())
            .Returns(new HttpResult<LicenseInfoDto>
            {
                Code = 200,
                Success = true,
                Data = expiredLicenseDto,
                Msg = "Success"
            });

        // await _licenseService.VerifyAuthorizationCodeAsync(authCode);

        // Act
        // var isValid = await _licenseService.IsLicenseValidAsync();

        // Assert
        // isValid.ShouldBeFalse();
    }
}

