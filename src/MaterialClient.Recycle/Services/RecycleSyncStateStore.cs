using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Recycle.Services;

/// <summary>
///     Recycle 同步状态存取助手。
///     支持两种承载方式：WeighingRecord.ExtraProperties（历史兼容）与 Waybill.ExtraProperties（新增 Waybill 级）。
/// </summary>
internal static class RecycleSyncStateStore
{
    private const string Prefix = "Recycle_";
    private const string SyncStatusKey = Prefix + "SyncStatus";
    private const string FailCountKey = Prefix + "FailCount";
    private const string FailMsgKey = Prefix + "FailMsg";
    private const string LastSyncTimeKey = Prefix + "LastSyncTime";

    #region WeighingRecord（历史兼容）

    public static SyncStatus GetSyncStatus(WeighingRecord record)
    {
        if (record.ExtraProperties.TryGetValue(SyncStatusKey, out var value) && value != null)
            return ToSyncStatus(value);
        return SyncStatus.Pending;
    }

    public static int GetFailCount(WeighingRecord record)
    {
        if (record.ExtraProperties.TryGetValue(FailCountKey, out var value) && value != null)
            return ToInt32(value);
        return 0;
    }

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
    }

    public static void MarkAbandoned(WeighingRecord record)
    {
        record.ExtraProperties[SyncStatusKey] = (int)SyncStatus.Failed;
    }

    #endregion

    #region Waybill（新增，Waybill 级同步）

    public static SyncStatus GetWaybillSyncStatus(Waybill waybill)
    {
        if (waybill.ExtraProperties.TryGetValue(SyncStatusKey, out var value) && value != null)
            return ToSyncStatus(value);
        return SyncStatus.Pending;
    }

    public static int GetWaybillFailCount(Waybill waybill)
    {
        if (waybill.ExtraProperties.TryGetValue(FailCountKey, out var value) && value != null)
            return ToInt32(value);
        return 0;
    }

    public static void SetWaybillSynced(Waybill waybill, DateTime now)
    {
        waybill.ExtraProperties[SyncStatusKey] = (int)SyncStatus.Synced;
        waybill.ExtraProperties[FailMsgKey] = null;
        waybill.ExtraProperties[LastSyncTimeKey] = now.ToString("O");
    }

    public static void SetWaybillFailed(Waybill waybill, int failCount, string failMsg, DateTime now)
    {
        waybill.ExtraProperties[FailCountKey] = failCount;
        waybill.ExtraProperties[FailMsgKey] = failMsg;
        waybill.ExtraProperties[LastSyncTimeKey] = now.ToString("O");
    }

    public static void MarkWaybillAbandoned(Waybill waybill)
    {
        waybill.ExtraProperties[SyncStatusKey] = (int)SyncStatus.Failed;
    }

    #endregion

    private static SyncStatus ToSyncStatus(object value)
    {
        try { return (SyncStatus)ToInt32(value); }
        catch { return SyncStatus.Pending; }
    }

    private static int ToInt32(object value)
    {
        try { return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture); }
        catch { return 0; }
    }
}
