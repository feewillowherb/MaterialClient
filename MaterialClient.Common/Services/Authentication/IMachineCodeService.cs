using System.Threading.Tasks;

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

