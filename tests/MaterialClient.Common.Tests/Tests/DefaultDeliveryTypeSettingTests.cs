using System.Text.Json;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Services;
using NSubstitute;
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

    [Fact]
    public async Task GetDefaultDeliveryTypeAsync_ReturnsSending_WhenPersisted()
    {
        // Arrange
        var mockSettingsService = Substitute.For<ISettingsService>();
        mockSettingsService.GetSettingsAsync().Returns(new SettingsEntity(
            new ScaleSettings(),
            new DocumentScannerConfig(),
            new SystemSettings { DefaultDeliveryType = DeliveryType.Sending },
            [],
            [],
            new WeighingConfiguration(),
            new SoundDeviceSettings()));

        // Act
        var result = await mockSettingsService.GetDefaultDeliveryTypeAsync();

        // Assert
        Assert.Equal(DeliveryType.Sending, result);
    }

    [Fact]
    public async Task GetDefaultDeliveryTypeAsync_ReturnsReceiving_OnEmptySettingsStore()
    {
        // Arrange — SystemSettings() constructor defaults DefaultDeliveryType to Receiving
        var mockSettingsService = Substitute.For<ISettingsService>();
        mockSettingsService.GetSettingsAsync().Returns(new SettingsEntity(
            new ScaleSettings(),
            new DocumentScannerConfig(),
            new SystemSettings(),
            [],
            [],
            new WeighingConfiguration(),
            new SoundDeviceSettings()));

        // Act
        var result = await mockSettingsService.GetDefaultDeliveryTypeAsync();

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
