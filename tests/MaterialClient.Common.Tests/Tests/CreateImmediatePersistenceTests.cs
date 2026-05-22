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

public class CreateImmediatePersistenceTests
{
    [Fact]
    public async Task CreateMaterialAsync_Should_Insert_When_LocalNotExists()
    {
        var materialRepo = Substitute.For<IRepository<Material, int>>();
        var unitRepo = Substitute.For<IRepository<MaterialUnit, int>>();
        var sessionRepo = Substitute.For<IRepository<UserSession, Guid>>();
        var api = Substitute.For<IMaterialPlatformApi>();

        var store = new List<Material>();
        materialRepo.FindAsync(Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(ci => store.FirstOrDefault(x => x.Id == ci.Arg<int>()));
        materialRepo
            .InsertAsync(Arg.Any<Material>(), true, default)
            .Returns(ci =>
            {
                var inserted = ci.Arg<Material>();
                store.RemoveAll(x => x.Id == inserted.Id);
                store.Add(inserted);
                return inserted;
            });
        materialRepo
            .UpdateAsync(Arg.Any<Material>(), true, default)
            .Returns(ci => ci.Arg<Material>());

        sessionRepo.GetListAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new List<UserSession> { CreateSession() });
        api.CreateMaterialByNameAsync(Arg.Any<CreateMaterialByNameInput>(), Arg.Any<CancellationToken>())
            .Returns(new ApiEnvelopeDto<MaterialGoodListResultDto>
            {
                Code = "OK",
                Data = new MaterialGoodListResultDto
                {
                    GoodsId = 101,
                    GoodsName = "新材料",
                    CoId = 1
                }
            });

        var service = new MaterialService(materialRepo, unitRepo, sessionRepo, api);

        var created = await service.CreateMaterialAsync("新材料");

        created.Id.ShouldBe(101);
        store.Any(x => x.Id == 101).ShouldBeTrue();
        await materialRepo.Received(1).InsertAsync(Arg.Is<Material>(m => m.Id == 101), true, default);
        await materialRepo.DidNotReceive().UpdateAsync(Arg.Any<Material>(), true, default);
    }

    [Fact]
    public async Task CreateProviderAsync_Should_Update_When_LocalExists()
    {
        var providerRepo = Substitute.For<IRepository<Provider, int>>();
        var sessionRepo = Substitute.For<IRepository<UserSession, Guid>>();
        var api = Substitute.For<IMaterialPlatformApi>();

        var store = new List<Provider> { new(201, 1, "旧供应商") { CoId = 1 } };
        providerRepo.FindAsync(Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(ci => store.FirstOrDefault(x => x.Id == ci.Arg<int>()));
        providerRepo
            .InsertAsync(Arg.Any<Provider>(), true, default)
            .Returns(ci =>
            {
                var inserted = ci.Arg<Provider>();
                store.RemoveAll(x => x.Id == inserted.Id);
                store.Add(inserted);
                return inserted;
            });
        providerRepo
            .UpdateAsync(Arg.Any<Provider>(), true, default)
            .Returns(ci =>
            {
                var updated = ci.Arg<Provider>();
                store.RemoveAll(x => x.Id == updated.Id);
                store.Add(updated);
                return updated;
            });

        sessionRepo.GetListAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new List<UserSession> { CreateSession() });
        api.CreateProviderAsync(Arg.Any<CreateProviderInput>(), Arg.Any<CancellationToken>())
            .Returns(new ApiEnvelopeDto<MaterialProviderListResultDto>
            {
                Code = "OK",
                Data = new MaterialProviderListResultDto
                {
                    ProviderId = 201,
                    ProviderType = 1,
                    ProviderName = "新供应商",
                    CoId = 1
                }
            });

        var service = new ProviderService(api, providerRepo, sessionRepo);

        var created = await service.CreateProviderAsync("新供应商", DeliveryType.Receiving);

        created.Id.ShouldBe(201);
        store.Single(x => x.Id == 201).ProviderName.ShouldBe("新供应商");
        await providerRepo.DidNotReceive().InsertAsync(Arg.Any<Provider>(), true, default);
        await providerRepo.Received(1).UpdateAsync(Arg.Is<Provider>(p => p.Id == 201 && p.ProviderName == "新供应商"), true, default);
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
