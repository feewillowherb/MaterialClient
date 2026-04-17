using Volo.Abp.Domain.Entities;

namespace MaterialClient.Common.Entities;

/// <summary>
///     实体类型枚举
/// </summary>
public enum SyncEntityType
{
    /// <summary>
    ///     物料
    /// </summary>
    Material = 0,

    /// <summary>
    ///     供应商
    /// </summary>
    Provider = 1
}

/// <summary>
///     同步状态枚举
/// </summary>
public enum SyncStatus
{
    /// <summary>
    ///     待同步
    /// </summary>
    Pending = 0,

    /// <summary>
    ///     已同步
    /// </summary>
    Applied = 1,

    /// <summary>
    ///     冲突
    /// </summary>
    Conflict = 2
}

/// <summary>
///     同步状态实体
///     用于追踪本地实体与服务端之间的同步状态
/// </summary>
public class SyncState : Entity<int>
{
    /// <summary>
    ///     构造函数（用于EF Core）
    /// </summary>
    protected SyncState()
    {
    }

    /// <summary>
    ///     构造函数（用于创建新的同步状态）
    /// </summary>
    public SyncState(SyncEntityType entityType, int entityId, long localVersion, Guid clientRequestId)
        : base(0)
    {
        EntityType = entityType;
        EntityId = entityId;
        LocalVersion = localVersion;
        ClientRequestId = clientRequestId;
        Status = SyncStatus.Pending;
        RetryCount = 0;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    ///     实体类型
    /// </summary>
    public SyncEntityType EntityType { get; set; }

    /// <summary>
    ///     实体ID
    /// </summary>
    public int EntityId { get; set; }

    /// <summary>
    ///     本地版本
    /// </summary>
    public long LocalVersion { get; set; }

    /// <summary>
    ///     服务端版本
    /// </summary>
    public long? ServerVersion { get; set; }

    /// <summary>
    ///     同步状态
    /// </summary>
    public SyncStatus Status { get; set; }

    /// <summary>
    ///     客户端请求ID（幂等键）
    /// </summary>
    public Guid ClientRequestId { get; set; }

    /// <summary>
    ///     最后上传尝试时间
    /// </summary>
    public DateTime? LastAttemptAt { get; set; }

    /// <summary>
    ///     上传失败次数
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    ///     创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    ///     最后更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    ///     标记为已应用
    /// </summary>
    public void MarkAsApplied(long serverVersion)
    {
        Status = SyncStatus.Applied;
        ServerVersion = serverVersion;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    ///     标记为冲突
    /// </summary>
    public void MarkAsConflict(long serverVersion)
    {
        Status = SyncStatus.Conflict;
        ServerVersion = serverVersion;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    ///     重置为待同步
    /// </summary>
    public void ResetToPending(long newLocalVersion)
    {
        Status = SyncStatus.Pending;
        LocalVersion = newLocalVersion;
        RetryCount = 0;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    ///     记录上传尝试
    /// </summary>
    public void RecordAttempt()
    {
        LastAttemptAt = DateTime.UtcNow;
        RetryCount++;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    ///     检查是否需要重试
    /// </summary>
    public bool ShouldRetry(int maxRetryCount = 5)
    {
        return Status == SyncStatus.Pending && RetryCount < maxRetryCount;
    }
}
