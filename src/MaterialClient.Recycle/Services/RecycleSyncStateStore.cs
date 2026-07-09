using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Recycle.Services;

/// <summary>
///     Recycle 同步状态存取助手。
///     设计约束「无新表（Recycle 复用 WeighingRecord + AttachmentFile）」下，
///     将 Recycle 上报同步状态承载于 <see cref="WeighingRecord.ExtraProperties" />（既有 JSON 列），
///     避免新增数据库表/迁移。键名加 <c>Recycle_</c> 前缀以与其它用途隔离。
/// </summary>
internal static class RecycleSyncStateStore
{
    private const string Prefix = "Recycle_";
    private const string SyncStatusKey = Prefix + "SyncStatus";
    private const string FailCountKey = Prefix + "FailCount";
    private const string FailMsgKey = Prefix + "FailMsg";
    private const string LastSyncTimeKey = Prefix + "LastSyncTime";

    /// <summary>读取同步状态，默认 <see cref="SyncStatus.Pending" />。</summary>
    public static SyncStatus GetSyncStatus(WeighingRecord record)
    {
        if (record.ExtraProperties.TryGetValue(SyncStatusKey, out var value) && value != null)
        {
            return ToSyncStatus(value);
        }

        return SyncStatus.Pending;
    }

    public static int GetFailCount(WeighingRecord record)
    {
        if (record.ExtraProperties.TryGetValue(FailCountKey, out var value) && value != null)
        {
            return ToInt32(value);
        }

        return 0;
    }

    public static string? GetFailMsg(WeighingRecord record)
    {
        return record.ExtraProperties.TryGetValue(FailMsgKey, out var value) ? value?.ToString() : null;
    }

    /// <summary>写入同步状态字段（FailCount/FailMsg/LastSyncTime 随状态一并更新）。</summary>
    public static void SetSynced(WeighingRecord record, DateTime now)
    {
        record.ExtraProperties[SyncStatusKey] = (int)SyncStatus.Synced;
        record.ExtraProperties[FailMsgKey] = null;
        record.ExtraProperties[LastSyncTimeKey] = now.ToString("O");
    }

    public static void SetFailed(WeighingRecord record, int failCount, string failMsg, DateTime now)
    {
        record.ExtraProperties[FailCountKey] = failCount;
        record.ExtraProperties[FailMsgKey] = failMsg;
        record.ExtraProperties[LastSyncTimeKey] = now.ToString("O");
        // SyncStatus 保留为 Pending（仍在重试队列）或最终 Failed（放弃），由调用方根据 failCount 决定。
    }

    public static void MarkAbandoned(WeighingRecord record)
    {
        record.ExtraProperties[SyncStatusKey] = (int)SyncStatus.Failed;
    }

    private static SyncStatus ToSyncStatus(object value)
    {
        try
        {
            return (SyncStatus)ToInt32(value);
        }
        catch
        {
            return SyncStatus.Pending;
        }
    }

    private static int ToInt32(object value)
    {
        try
        {
            return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0;
        }
    }
}
