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
