using System;
using System.Linq;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Common.Services.Authentication;

/// <summary>
/// 机器码服务接口
/// </summary>
public interface IMachineCodeService
{
    /// <summary>
    /// 获取当前机器的机器码（基于硬件标识的SHA256哈希）
    /// </summary>
    /// <returns>机器码（十六进制格式的SHA256哈希值）</returns>
    Task<string> GetMachineCodeAsync();
}

/// <summary>
/// 机器码服务实现
/// </summary>
public class MachineCodeService : IMachineCodeService, ITransientDependency
{
    public async Task<string> GetMachineCodeAsync()
    {
        return await Task.Run(() =>
        {
            var cpuId = GetCpuId();
            var boardId = GetBoardId();
            var macAddress = GetMacAddress();

            var combinedString = $"{cpuId}-{boardId}-{macAddress}";
            using (var sha256 = SHA256.Create())
            {
                var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combinedString));
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
        });
    }

    private string GetCpuId()
    {
        try
        {
            using (var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor"))
            {
                foreach (var obj in searcher.Get())
                {
                    return obj["ProcessorId"]?.ToString() ?? string.Empty;
                }
            }
        }
        catch
        {
            // Fallback to environment processor ID
            return Environment.ProcessorCount.ToString();
        }
        return string.Empty;
    }

    private string GetBoardId()
    {
        try
        {
            using (var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard"))
            {
                foreach (var obj in searcher.Get())
                {
                    return obj["SerialNumber"]?.ToString() ?? string.Empty;
                }
            }
        }
        catch
        {
            // Fallback to machine name
            return Environment.MachineName;
        }
        return string.Empty;
    }

    private string GetMacAddress()
    {
        try
        {
            using (var searcher = new ManagementObjectSearcher("SELECT MACAddress FROM Win32_NetworkAdapter WHERE MACAddress IS NOT NULL"))
            {
                foreach (var obj in searcher.Get())
                {
                    return obj["MACAddress"]?.ToString() ?? string.Empty;
                }
            }
        }
        catch
        {
            // Fallback to user name
            return Environment.UserName;
        }
        return string.Empty;
    }
}
