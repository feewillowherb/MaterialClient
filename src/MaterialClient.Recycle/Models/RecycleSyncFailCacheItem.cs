using Volo.Abp.Caching;

namespace MaterialClient.Recycle.Models;

/// <summary>
///     Recycle 上报失败冷却缓存项（按 WaybillId 作为缓存键）。
/// </summary>
[CacheName("RecycleSyncFail")]
public class RecycleSyncFailCacheItem
{
    public string? FailMsg { get; set; }
}
