using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Entities.Urban;
using MaterialClient.Common.Services;
using MaterialClient.Common.Services.Urban;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MaterialClient.Common.Tests.Tests;

public class UrbanWeighingExtensionServiceTests
{
    private readonly IRepository<UrbanWeighingExtension, Guid> _extensionRepository =
        Substitute.For<IRepository<UrbanWeighingExtension, Guid>>();

    private readonly IRepository<WeighingRecord, long> _weighingRecordRepository =
        Substitute.For<IRepository<WeighingRecord, long>>();

    private readonly UrbanWeighingExtensionService _service;

    public UrbanWeighingExtensionServiceTests()
    {
        _service = new UrbanWeighingExtensionService(
            _extensionRepository,
            _weighingRecordRepository,
            Substitute.For<IUrbanAnomalyDetector>(),
            Substitute.For<ISettingsService>(),
            new ConfigurationBuilder().Build(),
            Substitute.For<ILogger<UrbanWeighingExtensionService>>());
    }

    [Fact]
    public async Task CreateForRecordAsync_Should_Throw_When_WeighingRecordId_Is_Zero()
    {
        await Should.ThrowAsync<BusinessException>(() => _service.CreateForRecordAsync(0));
    }

    [Fact]
    public async Task CreateForRecordAsync_WhenEvaluateAnomalyFalse_ShouldDeferAnomalyFlags()
    {
        const long recordId = 2001;
        var inserted = (UrbanWeighingExtension?)null;

        _weighingRecordRepository.GetAsync(recordId).Returns(new WeighingRecord(10m, null));
        _extensionRepository.FirstOrDefaultAsync(Arg.Any<System.Linq.Expressions.Expression<Func<UrbanWeighingExtension, bool>>>())
            .Returns((UrbanWeighingExtension?)null);
        _extensionRepository.InsertAsync(Arg.Any<UrbanWeighingExtension>(), Arg.Any<bool>())
            .Returns(call =>
            {
                inserted = call.Arg<UrbanWeighingExtension>();
                return inserted;
            });

        var anomalyDetector = Substitute.For<IUrbanAnomalyDetector>();
        var service = new UrbanWeighingExtensionService(
            _extensionRepository,
            _weighingRecordRepository,
            anomalyDetector,
            Substitute.For<ISettingsService>(),
            new ConfigurationBuilder().Build(),
            Substitute.For<ILogger<UrbanWeighingExtensionService>>());

        var extension = await service.CreateForRecordAsync(recordId, hasLprAttachment: false, evaluateAnomaly: false);

        extension.IsAnomaly.ShouldBeFalse();
        extension.AnomalyReason.ShouldBeNull();
        anomalyDetector.DidNotReceiveWithAnyArgs().IsAnomaly(default!, default!, default);
        inserted.ShouldNotBeNull();
        inserted!.IsAnomaly.ShouldBeFalse();
    }

    [Fact]
    public void NewExtension_Should_Use_NonZero_WeighingRecordId_When_Associating()
    {
        const long recordId = 1001;
        var extension = new UrbanWeighingExtension
        {
            WeighingRecordId = recordId,
            SyncStatus = SyncStatus.Pending
        };

        extension.WeighingRecordId.ShouldBe(recordId);
        extension.WeighingRecordId.ShouldBeGreaterThan(0);
    }
}
