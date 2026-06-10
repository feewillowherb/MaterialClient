using System.Text.Json;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities.Enums;
using Xunit;

namespace MaterialClient.Common.Tests.IntegrationTests;

/// <summary>
///     JSON 序列化/反序列化兼容性测试
/// </summary>
public class HikvisionLprConfigJsonTests
{
    [Fact]
    public void Deserialize_OldJsonWithoutHikvisionFields_ShouldReturnConfigsWithNullFields()
    {
        // Arrange - 旧版本 JSON (不包含海康威视字段)
        var oldJson = @"
        [
            {
                ""Name"": ""lpr_device_1"",
                ""Ip"": ""192.168.1.100"",
                ""Direction"": 0
            },
            {
                ""Name"": ""lpr_device_2"",
                ""Ip"": ""192.168.1.101"",
                ""Direction"": 1
            }
        ]";

        // Act
        var configs = JsonSerializer.Deserialize<List<LicensePlateRecognitionConfig>>(oldJson);

        // Assert
        Assert.NotNull(configs);
        Assert.Equal(2, configs.Count);

        var config1 = configs[0];
        Assert.Equal("lpr_device_1", config1.Name);
        Assert.Equal("192.168.1.100", config1.Ip);
        Assert.Equal(LicensePlateDirection.A, config1.Direction);
        Assert.Null(config1.UserName);
        Assert.Null(config1.Password);
        Assert.Null(config1.Port);
        Assert.Null(config1.Channel);

        var config2 = configs[1];
        Assert.Equal("lpr_device_2", config2.Name);
        Assert.Equal("192.168.1.101", config2.Ip);
        Assert.Equal(LicensePlateDirection.B, config2.Direction);
        Assert.Null(config2.UserName);
        Assert.Null(config2.Password);
        Assert.Null(config2.Port);
        Assert.Null(config2.Channel);
    }

    [Fact]
    public void Serialize_NewConfigWithHikvisionFields_ShouldIncludeAllFields()
    {
        // Arrange
        var config = new LicensePlateRecognitionConfig
        {
            Name = "lpr_device_1",
            Ip = "192.168.1.100",
            Direction = LicensePlateDirection.A,
            UserName = "admin",
            Password = "password123",
            Port = "8000",
            Channel = "1"
        };

        // Act
        var json = JsonSerializer.Serialize(config);

        // Assert
        Assert.Contains("\"Name\"", json);
        Assert.Contains("\"Ip\"", json);
        Assert.Contains("\"Direction\"", json);
        Assert.Contains("\"UserName\"", json);
        Assert.Contains("\"Password\"", json);
        Assert.Contains("\"Port\"", json);
        Assert.Contains("\"Channel\"", json);

        // 验证值
        Assert.Contains("lpr_device_1", json);
        Assert.Contains("admin", json);
        Assert.Contains("password123", json);
        Assert.Contains("8000", json);
        Assert.Contains("1", json);
    }

    [Fact]
    public void RoundTrip_ConfigWithNullFields_ShouldPreserveNullValues()
    {
        // Arrange
        var originalConfig = new LicensePlateRecognitionConfig
        {
            Name = "lpr_device_1",
            Ip = "192.168.1.100",
            Direction = LicensePlateDirection.A,
            UserName = null,
            Password = null,
            Port = null,
            Channel = null
        };

        // Act
        var json = JsonSerializer.Serialize(originalConfig);
        var deserializedConfig = JsonSerializer.Deserialize<LicensePlateRecognitionConfig>(json);

        // Assert
        Assert.NotNull(deserializedConfig);
        Assert.Equal(originalConfig.Name, deserializedConfig.Name);
        Assert.Equal(originalConfig.Ip, deserializedConfig.Ip);
        Assert.Equal(originalConfig.Direction, deserializedConfig.Direction);
        Assert.Null(deserializedConfig.UserName);
        Assert.Null(deserializedConfig.Password);
        Assert.Null(deserializedConfig.Port);
        Assert.Null(deserializedConfig.Channel);
    }

    [Fact]
    public void LoadSettings_MixedOldAndNewConfigs_ShouldDeserializeCorrectly()
    {
        // Arrange - 混合旧配置和新配置
        var mixedJson = @"
        [
            {
                ""Name"": ""old_device"",
                ""Ip"": ""192.168.1.100"",
                ""Direction"": 0
            },
            {
                ""Name"": ""new_device"",
                ""Ip"": ""192.168.1.101"",
                ""Direction"": 1,
                ""UserName"": ""admin"",
                ""Password"": ""password123"",
                ""Port"": ""8000"",
                ""Channel"": ""1""
            }
        ]";

        // Act
        var configs = JsonSerializer.Deserialize<List<LicensePlateRecognitionConfig>>(mixedJson);

        // Assert
        Assert.NotNull(configs);
        Assert.Equal(2, configs.Count);

        // 验证旧配置
        var oldConfig = configs[0];
        Assert.Equal("old_device", oldConfig.Name);
        Assert.Null(oldConfig.UserName);
        Assert.Null(oldConfig.Password);
        Assert.Null(oldConfig.Port);
        Assert.Null(oldConfig.Channel);

        // 验证新配置
        var newConfig = configs[1];
        Assert.Equal("new_device", newConfig.Name);
        Assert.Equal("admin", newConfig.UserName);
        Assert.Equal("password123", newConfig.Password);
        Assert.Equal("8000", newConfig.Port);
        Assert.Equal("1", newConfig.Channel);
    }

    [Fact]
    public void Serialize_ConfigList_ShouldProduceValidJson()
    {
        // Arrange
        var configs = new List<LicensePlateRecognitionConfig>
        {
            new()
            {
                Name = "device1",
                Ip = "192.168.1.100",
                Direction = LicensePlateDirection.A,
                UserName = "admin",
                Password = "password123",
                Port = "8000",
                Channel = "1"
            },
            new()
            {
                Name = "device2",
                Ip = "192.168.1.101",
                Direction = LicensePlateDirection.B,
                UserName = "user",
                Password = "pass",
                Port = "8001",
                Channel = "2"
            }
        };

        // Act
        var json = JsonSerializer.Serialize(configs);

        // Assert
        Assert.NotNull(json);
        Assert.Contains("device1", json);
        Assert.Contains("device2", json);
        Assert.Contains("admin", json);
        Assert.Contains("user", json);
    }
}
