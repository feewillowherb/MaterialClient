using MaterialClient.Common.Api;
using MaterialClient.Common.Api.Dtos;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Services;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MaterialClient.Common.Tests.Tests;

/// <summary>
///     单测：ProviderService.CreateProviderAsync 回填本地 Address；
///     MaterialProviderListResultDto.ToEntity 不覆盖 Address（远端无此字段）。
/// </summary>
public class ProviderAddressBackfillTests
{
    [Fact]
    public async Task CreateProviderAsync_Backfills_Address_Before_Local_Upsert()
    {
        var providerRepo = Substitute.For<IRepository<Provider, int>>();
        var sessionRepo = Substitute.For<IRepository<UserSession, Guid>>();
        var api = Substitute.For<IMaterialPlatformApi>();

        var store = new List<Provider>();
        Provider? captured = null;
        providerRepo.FindAsync(Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(ci => store.FirstOrDefault(x => x.Id == ci.Arg<int>()));
        providerRepo
            .InsertAsync(Arg.Any<Provider>(), true, default)
            .Returns(ci =>
            {
                var inserted = ci.Arg<Provider>();
                captured = inserted; // 捕获即将 upsert 的实体，验证 Address 已回填
                store.Add(inserted);
                return inserted;
            });

        sessionRepo.GetListAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new List<UserSession> { CreateSession() });
        api.CreateProviderAsync(Arg.Any<CreateProviderInput>(), Arg.Any<CancellationToken>())
            .Returns(new ApiEnvelopeDto<MaterialProviderListResultDto>
            {
                Code = "OK",
                Data = new MaterialProviderListResultDto
                {
                    ProviderId = 301,
                    ProviderType = 1,
                    ProviderName = "回收运输公司",
                    CoId = 1
                }
            });

        var service = new ProviderService(api, providerRepo, sessionRepo);

        var created = await service.CreateProviderAsync(
            "回收运输公司",
            DeliveryType.Sending,
            address: "杭州市西湖区某路 1 号");

        // 远端创建返回的实体在本地 upsert 前已回填 Address
        created.Address.ShouldBe("杭州市西湖区某路 1 号");
        captured.ShouldNotBeNull();
        captured!.Address.ShouldBe("杭州市西湖区某路 1 号");
        await providerRepo.Received(1).InsertAsync(
            Arg.Is<Provider>(p => p.Id == 301 && p.Address == "杭州市西湖区某路 1 号"), true, default);
    }

    [Fact]
    public async Task CreateProviderAsync_Without_Address_Keeps_Address_Null()
    {
        var providerRepo = Substitute.For<IRepository<Provider, int>>();
        var sessionRepo = Substitute.For<IRepository<UserSession, Guid>>();
        var api = Substitute.For<IMaterialPlatformApi>();

        providerRepo.FindAsync(Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((Provider?)null);
        providerRepo
            .InsertAsync(Arg.Any<Provider>(), true, default)
            .Returns(ci => ci.Arg<Provider>());

        sessionRepo.GetListAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new List<UserSession> { CreateSession() });
        api.CreateProviderAsync(Arg.Any<CreateProviderInput>(), Arg.Any<CancellationToken>())
            .Returns(new ApiEnvelopeDto<MaterialProviderListResultDto>
            {
                Code = "OK",
                Data = new MaterialProviderListResultDto
                {
                    ProviderId = 302,
                    ProviderName = "无地址供应商",
                    CoId = 1
                }
            });

        var service = new ProviderService(api, providerRepo, sessionRepo);

        var created = await service.CreateProviderAsync("无地址供应商", DeliveryType.Receiving);

        created.Address.ShouldBeNull();
    }

    [Fact]
    public void ToEntity_Does_Not_Override_Address()
    {
        // 远端 DTO 无 Address 字段；ToEntity 创建的 Provider.Address SHALL 保持默认 null
        var dto = new MaterialProviderListResultDto
        {
            ProviderId = 303,
            ProviderName = "远端供应商",
            CoId = 1
        };

        var provider = MaterialProviderListResultDto.ToEntity(dto);

        provider.Id.ShouldBe(303);
        provider.Address.ShouldBeNull();
    }

    private static UserSession CreateSession()
    {
        return new UserSession(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            "test",
            "test",
            Guid.NewGuid(),
            "token",
            false,
            false,
            0,
            0,
            0,
            "p",
            1,
            "c",
            "http://localhost",
            DateTime.UtcNow.AddDays(1));
    }
}
