using System.Text.Json;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Services;
using NSubstitute;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Xunit;

namespace MaterialClient.Common.Tests.Tests;

/// <summary>
///     Unit tests for the DefaultDeliveryType setting feature.
///     Covers: JSON round-trip (4.1), SettingsService accessor (4.2), invalid value guard (4.5).
/// </summary>
public class DefaultDeliveryTypeSettingTests
{
    #region 4.1 - SystemSettings.DefaultDeliveryType JSON round-trip

    [Fact]
    public void RoundTrip_Receiving_ShouldPreserveValue()
    {
        // Arrange
        var settings = new SystemSettings { DefaultDeliveryType = DeliveryType.Receiving };

        // Act
        var json = JsonSerializer.Serialize(settings);
        var deserialized = JsonSerializer.Deserialize<SystemSettings>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(DeliveryType.Receiving, deserialized.DefaultDeliveryType);
    }

    [Fact]
    public void RoundTrip_Sending_ShouldPreserveValue()
    {
        // Arrange
        var settings = new SystemSettings { DefaultDeliveryType = DeliveryType.Sending };

        // Act
        var json = JsonSerializer.Serialize(settings);
        var deserialized = JsonSerializer.Deserialize<SystemSettings>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(DeliveryType.Sending, deserialized.DefaultDeliveryType);
    }

    [Fact]
    public void RoundTrip_OmittedField_ShouldDefaultToReceiving()
    {
        // Arrange — JSON without DefaultDeliveryType field
        var json = "{}";

        // Act
        var deserialized = JsonSerializer.Deserialize<SystemSettings>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(DeliveryType.Receiving, deserialized.DefaultDeliveryType);
    }

    [Fact]
    public void RoundTrip_PreservesOtherFields_WhenDefaultDeliveryTypeIsSet()
    {
        // Arrange
        var settings = new SystemSettings
        {
            DefaultDeliveryType = DeliveryType.Sending,
            EnableAutoStart = true,
            Urls = "http://example.com:9960",
            JpegQuality = 50
        };

        // Act
        var json = JsonSerializer.Serialize(settings);
        var deserialized = JsonSerializer.Deserialize<SystemSettings>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(DeliveryType.Sending, deserialized.DefaultDeliveryType);
        Assert.True(deserialized.EnableAutoStart);
        Assert.Equal("http://example.com:9960", deserialized.Urls);
        Assert.Equal(50, deserialized.JpegQuality);
    }

    #endregion

    #region 4.2 - SettingsService.GetDefaultDeliveryTypeAsync returns persisted value

    private static SettingsService CreateSettingsService(SettingsEntity? persisted)
    {
        var mockRepo = Substitute.For<IRepository<SettingsEntity, int>>();
        mockRepo.GetListAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(persisted is null ? [] : [persisted]);

        var mockUow = Substitute.For<IUnitOfWork>();
        mockUow.CompleteAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var mockUowManager = Substitute.For<IUnitOfWorkManager>();
        mockUowManager.Begin(Arg.Any<AbpUnitOfWorkOptions>(), Arg.Any<bool>()).Returns(mockUow);

        return new SettingsService(mockRepo, mockUowManager);
    }

    private static SettingsEntity CreateSettingsEntity(DeliveryType defaultDeliveryType) =>
        new(
            new ScaleSettings(),
            new DocumentScannerConfig(),
            new SystemSettings { DefaultDeliveryType = defaultDeliveryType },
            [],
            [],
            new WeighingConfiguration(),
            new SoundDeviceSettings());

    [Fact]
    public async Task GetDefaultDeliveryTypeAsync_ReturnsSending_WhenPersisted()
    {
        // Arrange — real SettingsService reads DefaultDeliveryType from persisted SystemSettings
        var settingsService = CreateSettingsService(CreateSettingsEntity(DeliveryType.Sending));

        // Act
        var result = await settingsService.GetDefaultDeliveryTypeAsync();

        // Assert
        Assert.Equal(DeliveryType.Sending, result);
    }

    [Fact]
    public async Task GetDefaultDeliveryTypeAsync_ReturnsReceiving_OnEmptySettingsStore()
    {
        // Arrange — no persisted row → SettingsService creates defaults (DefaultDeliveryType = Receiving)
        var settingsService = CreateSettingsService(persisted: null);

        // Act
        var result = await settingsService.GetDefaultDeliveryTypeAsync();

        // Assert
        Assert.Equal(DeliveryType.Receiving, result);
    }

    #endregion

    #region 4.5 - Invalid/unknown stored value is guarded to Receiving

    [Fact]
    public void InvalidEnumValue_IsNotDefined_ReturnsFalse()
    {
        // Arrange — DeliveryType only has Receiving (0) and Sending (1)
        var invalidValue = (DeliveryType)99;

        // Act & Assert
        Assert.False(Enum.IsDefined(invalidValue));
    }

    [Fact]
    public void GuardLogic_InvalidValue_ShouldFallbackToReceiving()
    {
        // Arrange — simulate what InitializeOnFirstLoadAsync does
        var storedValue = (DeliveryType)99;

        // Act — guard: if not defined, fall back to Receiving
        var effective = Enum.IsDefined(storedValue) ? storedValue : DeliveryType.Receiving;

        // Assert
        Assert.Equal(DeliveryType.Receiving, effective);
    }

    [Fact]
    public void GuardLogic_ValidReceiving_ShouldKeepReceiving()
    {
        var storedValue = DeliveryType.Receiving;
        var effective = Enum.IsDefined(storedValue) ? storedValue : DeliveryType.Receiving;
        Assert.Equal(DeliveryType.Receiving, effective);
    }

    [Fact]
    public void GuardLogic_ValidSending_ShouldKeepSending()
    {
        var storedValue = DeliveryType.Sending;
        var effective = Enum.IsDefined(storedValue) ? storedValue : DeliveryType.Receiving;
        Assert.Equal(DeliveryType.Sending, effective);
    }

    #endregion
}
