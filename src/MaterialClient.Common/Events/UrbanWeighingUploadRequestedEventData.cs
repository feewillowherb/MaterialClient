namespace MaterialClient.Common.Events;

/// <summary>
///     城管称重记录审批后请求立即上云（由后台 Handler 处理，不在 UI 线程阻塞 HTTP）。
/// </summary>
public class UrbanWeighingUploadRequestedEventData
{
    public UrbanWeighingUploadRequestedEventData(long weighingRecordId)
    {
        WeighingRecordId = weighingRecordId;
    }

    /// <summary>
    ///     待上云的称重记录 ID
    /// </summary>
    public long WeighingRecordId { get; }
}
