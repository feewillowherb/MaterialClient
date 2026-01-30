using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities.Enums;
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
            Direction = LicensePlateDirection.In,
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
            Direction = LicensePlateDirection.In,
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
}
