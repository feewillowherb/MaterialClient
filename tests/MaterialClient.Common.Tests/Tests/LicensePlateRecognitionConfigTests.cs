using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities.Enums;
using System.Collections.Generic;
using Xunit;

namespace MaterialClient.Common.Tests.Tests;

/// <summary>
///     LicensePlateRecognitionConfig 单元测试
/// </summary>
public class LicensePlateRecognitionConfigTests
{
    [Fact]
    public void UserName_ShouldBeSettableAndGettable()
    {
        // Arrange
        var config = new LicensePlateRecognitionConfig();
        var expectedUserName = "admin";

        // Act
        config.UserName = expectedUserName;
        var actualUserName = config.UserName;

        // Assert
        Assert.Equal(expectedUserName, actualUserName);
    }

    [Fact]
    public void Password_ShouldBeSettableAndGettable()
    {
        // Arrange
        var config = new LicensePlateRecognitionConfig();
        var expectedPassword = "password123";

        // Act
        config.Password = expectedPassword;
        var actualPassword = config.Password;

        // Assert
        Assert.Equal(expectedPassword, actualPassword);
    }

    [Fact]
    public void Port_ShouldBeSettableAndGettable()
    {
        // Arrange
        var config = new LicensePlateRecognitionConfig();
        var expectedPort = "8000";

        // Act
        config.Port = expectedPort;
        var actualPort = config.Port;

        // Assert
        Assert.Equal(expectedPort, actualPort);
    }

    [Fact]
    public void Channel_ShouldBeSettableAndGettable()
    {
        // Arrange
        var config = new LicensePlateRecognitionConfig();
        var expectedChannel = "1";

        // Act
        config.Channel = expectedChannel;
        var actualChannel = config.Channel;

        // Assert
        Assert.Equal(expectedChannel, actualChannel);
    }

    [Fact]
    public void HikvisionFields_DefaultToNull()
    {
        // Arrange & Act
        var config = new LicensePlateRecognitionConfig();

        // Assert
        Assert.Null(config.UserName);
        Assert.Null(config.Password);
        Assert.Null(config.Port);
        Assert.Null(config.Channel);
    }

    [Fact]
    public void IsValid_OnlyRequiresNameAndIp_WhenHikvisionFieldsAreNull()
    {
        // Arrange
        var config = new LicensePlateRecognitionConfig
        {
            Name = "device1",
            Ip = "192.168.1.100",
            Direction = LicensePlateDirection.A,
            // 海康威视字段为 null
            UserName = null,
            Password = null,
            Port = null,
            Channel = null
        };

        // Act
        var isValid = config.IsValid();

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void IsValid_OnlyRequiresNameAndIp_WhenHikvisionFieldsAreSet()
    {
        // Arrange
        var config = new LicensePlateRecognitionConfig
        {
            Name = "device1",
            Ip = "192.168.1.100",
            Direction = LicensePlateDirection.A,
            UserName = "admin",
            Password = "password123",
            Port = "8000",
            Channel = "1"
        };

        // Act
        var isValid = config.IsValid();

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void IsValid_ReturnsFalse_WhenNameIsEmpty()
    {
        // Arrange
        var config = new LicensePlateRecognitionConfig
        {
            Name = "",
            Ip = "192.168.1.100",
            UserName = "admin",
            Password = "password123",
            Port = "8000",
            Channel = "1"
        };

        // Act
        var isValid = config.IsValid();

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void IsValid_ReturnsFalse_WhenIpIsEmpty()
    {
        // Arrange
        var config = new LicensePlateRecognitionConfig
        {
            Name = "device1",
            Ip = "",
            UserName = "admin",
            Password = "password123",
            Port = "8000",
            Channel = "1"
        };

        // Act
        var isValid = config.IsValid();

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void ApplyLegacyDeviceType_FillsNullOnly()
    {
        var missing = new LicensePlateRecognitionConfig { Name = "a", Ip = "1.1.1.1" };
        missing.ApplyLegacyDeviceType(LprDeviceType.Vzvision);
        Assert.Equal(LprDeviceType.Vzvision, missing.DeviceType);

        var existing = new LicensePlateRecognitionConfig
        {
            Name = "b",
            Ip = "1.1.1.2",
            DeviceType = LprDeviceType.Hikvision
        };
        existing.ApplyLegacyDeviceType(LprDeviceType.Vzvision);
        Assert.Equal(LprDeviceType.Hikvision, existing.DeviceType);
    }

    [Fact]
    public void JsonRoundTrip_PersistsMixedDeviceTypes()
    {
        var list = new List<LicensePlateRecognitionConfig>
        {
            LicensePlateRecognitionConfig.FromUi("hik", "10.0.0.1", LicensePlateDirection.A, "admin", "", "8000", "1", false, "1", LprDeviceType.Hikvision),
            LicensePlateRecognitionConfig.FromUi("vz", "10.0.0.2", LicensePlateDirection.B, "admin", "", "80", null, true, "1", LprDeviceType.Vzvision)
        };

        var json = System.Text.Json.JsonSerializer.Serialize(list);
        var loaded = System.Text.Json.JsonSerializer.Deserialize<List<LicensePlateRecognitionConfig>>(json);
        Assert.NotNull(loaded);
        Assert.Equal(LprDeviceType.Hikvision, loaded[0].DeviceType);
        Assert.Equal(LprDeviceType.Vzvision, loaded[1].DeviceType);
    }

    [Fact]
    public void JsonMissingDeviceType_DeserializesAsNull_ThenLegacyBackfill()
    {
        const string json = """[{"Name":"old","Ip":"10.0.0.3","Direction":0}]""";
        var loaded = System.Text.Json.JsonSerializer.Deserialize<List<LicensePlateRecognitionConfig>>(json);
        Assert.NotNull(loaded);
        Assert.Null(loaded[0].DeviceType);
        loaded[0].ApplyLegacyDeviceType(LprDeviceType.Vzvision);
        Assert.Equal(LprDeviceType.Vzvision, loaded[0].ResolvedDeviceType);
    }

    [Fact]
    public void DeviceManagerStart_WouldEnableBothSdks_WhenMixedValidRows()
    {
        // DeviceManagerService.StartAsync 对每种厂商独立调用 AnyValidOfType 后再 Start*LprServiceAsync
        var configs = new List<LicensePlateRecognitionConfig>
        {
            new() { Name = "h", Ip = "1.1.1.1", DeviceType = LprDeviceType.Hikvision },
            new() { Name = "v", Ip = "1.1.1.2", DeviceType = LprDeviceType.Vzvision }
        };

        Assert.True(LicensePlateRecognitionConfig.AnyValidOfType(configs, LprDeviceType.Hikvision));
        Assert.True(LicensePlateRecognitionConfig.AnyValidOfType(configs, LprDeviceType.Vzvision));
        Assert.False(LicensePlateRecognitionConfig.AnyValidOfType(configs, LprDeviceType.Huaxiazhixin));
    }

    [Fact]
    public void EchoLegacyLprDeviceType_UsesFirstValidRow()
    {
        var settings = new SystemSettings { LprDeviceType = LprDeviceType.Hikvision };
        var configs = new List<LicensePlateRecognitionConfig>
        {
            new() { Name = "", Ip = "", DeviceType = LprDeviceType.Hikvision },
            new() { Name = "v", Ip = "1.1.1.2", DeviceType = LprDeviceType.Vzvision }
        };
        settings.EchoLegacyLprDeviceType(configs);
        Assert.Equal(LprDeviceType.Vzvision, settings.LprDeviceType);
    }
}
