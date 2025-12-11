using MaterialClient.Common.Services.Authentication;
using Shouldly;
using Xunit;

namespace MaterialClient.Common.Tests;

/// <summary>
/// 机器码服务单元测试
/// </summary>
public class MachineCodeServiceTests
{
    private readonly MachineCodeService _machineCodeService;

    public MachineCodeServiceTests()
    {
        _machineCodeService = new MachineCodeService();
    }

    [Fact]
    public async Task GetMachineCodeAsync_ShouldReturnNonEmptyString()
    {
        // Act
        var machineCode = _machineCodeService.GetMachineCode();

        // Assert
        machineCode.ShouldNotBeNullOrEmpty();
        machineCode.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task GetMachineCodeAsync_ShouldReturnHexString()
    {
        // Act
        var machineCode = _machineCodeService.GetMachineCode();

        // Assert - should be valid hex string (SHA256 = 64 hex chars)
        machineCode.Length.ShouldBe(64);
        machineCode.All(c => "0123456789abcdef".Contains(c)).ShouldBeTrue();
    }

    [Fact]
    public async Task GetMachineCodeAsync_ShouldReturnConsistentValue()
    {
        // Act - call multiple times
        var machineCode1 = _machineCodeService.GetMachineCode();
        var machineCode2 = _machineCodeService.GetMachineCode();

        // Assert - should return same value for same machine
        machineCode1.ShouldBe(machineCode2);
    }

    [Fact]
    public async Task GetMachineCodeAsync_ShouldBeDeterministic()
    {
        // Arrange
        var service1 = new MachineCodeService();
        var service2 = new MachineCodeService();

        // Act
        var code1 = service1.GetMachineCode();
        var code2 = service2.GetMachineCode();

        // Assert - multiple instances should return same code for same machine
        code1.ShouldBe(code2);
    }
}