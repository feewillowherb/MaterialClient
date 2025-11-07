using System.Management;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace MaterialClient.Common.Services.Authentication;

/// <summary>
/// 机器码服务实现
/// </summary>
public class MachineCodeService : IMachineCodeService
{
    private readonly ILogger<MachineCodeService> _logger;
    
    public MachineCodeService(ILogger<MachineCodeService> logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// 获取机器码（CPU ID + 主板序列号 + MAC地址的SHA256哈希）
    /// </summary>
    public string GetMachineCode()
    {
        var cpuId = GetCpuId();
        var boardSerial = GetBoardSerialNumber();
        var macAddress = GetFirstMacAddress();
        
        var combined = $"{cpuId}|{boardSerial}|{macAddress}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(combined));
        
        var machineCode = Convert.ToBase64String(hash);
        
        _logger.LogInformation("Machine code generated successfully (length: {Length})", machineCode.Length);
        
        return machineCode;
    }
    
    private string GetCpuId()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT ProcessorId FROM Win32_Processor");
            
            foreach (var obj in searcher.Get())
            {
                var cpuId = obj["ProcessorId"]?.ToString() ?? string.Empty;
                if (!string.IsNullOrEmpty(cpuId))
                {
                    _logger.LogDebug("CPU ID retrieved successfully");
                    return cpuId;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve CPU ID, using empty string");
        }
        
        return string.Empty;
    }
    
    private string GetBoardSerialNumber()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT SerialNumber FROM Win32_BaseBoard");
            
            foreach (var obj in searcher.Get())
            {
                var serial = obj["SerialNumber"]?.ToString() ?? string.Empty;
                if (!string.IsNullOrEmpty(serial))
                {
                    _logger.LogDebug("Board serial number retrieved successfully");
                    return serial;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve board serial number, using empty string");
        }
        
        return string.Empty;
    }
    
    private string GetFirstMacAddress()
    {
        try
        {
            var nics = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback
                         && n.OperationalStatus == OperationalStatus.Up)
                .OrderBy(n => n.Name)
                .ToList();
            
            if (nics.Any())
            {
                var macAddress = nics.First().GetPhysicalAddress().ToString();
                _logger.LogDebug("MAC address retrieved successfully");
                return macAddress;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve MAC address, using empty string");
        }
        
        return string.Empty;
    }
}

