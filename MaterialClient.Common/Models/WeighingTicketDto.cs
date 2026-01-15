namespace MaterialClient.Common.Models;

/// <summary>
/// DTO for weighing ticket data (称重计量单)
/// Used for printing weighing tickets to PDF or physical printers
/// </summary>
public class WeighingTicketDto
{
    // Header Information
    /// <summary>公司名称 - Company Name</summary>
    public string CompanyName { get; set; } = "杭州萧山城市运营管理有限公司";

    /// <summary>单据标题 - Document Title</summary>
    public string DocumentTitle { get; set; } = "东部资源化处置点称重计量单";

    /// <summary>打印时间 - Print Time</summary>
    public DateTime PrintTime { get; set; }

    /// <summary>流水号 - Serial Number</summary>
    public string SerialNumber { get; set; } = string.Empty;

    /// <summary>计量单位 - Measurement Unit</summary>
    public string MeasurementUnit { get; set; } = "公斤";

    // Vehicle and Goods Information
    /// <summary>车号 - Vehicle Number</summary>
    public string VehicleNumber { get; set; } = string.Empty;

    /// <summary>货名 - Goods Name</summary>
    public string GoodsName { get; set; } = string.Empty;

    /// <summary>发货单位 - Shipping Unit</summary>
    public string ShippingUnit { get; set; } = string.Empty;

    /// <summary>收货单位 - Receiving Unit</summary>
    public string ReceivingUnit { get; set; } = string.Empty;

    // Time Information
    /// <summary>进场时间 - Entry Time</summary>
    public DateTime EntryTime { get; set; }

    /// <summary>出场时间 - Exit Time</summary>
    public DateTime ExitTime { get; set; }

    // Weight Information
    /// <summary>毛重 - Gross Weight (kg)</summary>
    public decimal GrossWeight { get; set; }

    /// <summary>皮重 - Tare Weight (kg)</summary>
    public decimal TareWeight { get; set; }

    /// <summary>净重 - Net Weight (kg)</summary>
    public decimal NetWeight { get; set; }

    // Additional Information
    /// <summary>类型 - Type</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>备注 - Remarks</summary>
    public string Remarks { get; set; } = string.Empty;

    /// <summary>联单编号 - Manifest Number</summary>
    public string ManifestNumber { get; set; } = string.Empty;

    /// <summary>所属镇街 - Town/Street</summary>
    public string TownStreet { get; set; } = string.Empty;

    // Signatures
    /// <summary>司磅员签字 - Weigher Signature</summary>
    public string WeigherSignature { get; set; } = string.Empty;

    /// <summary>驾驶员签字 - Driver Signature</summary>
    public string DriverSignature { get; set; } = string.Empty;

    /// <summary>监磅员签字 - Supervisor Signature</summary>
    public string SupervisorSignature { get; set; } = string.Empty;

    /// <summary>
    /// Create a sample DTO with test data for testing and demonstration
    /// </summary>
    public static WeighingTicketDto CreateSample()
    {
        return new WeighingTicketDto
        {
            CompanyName = "杭州萧山城市运营管理有限公司",
            DocumentTitle = "东部资源化处置点称重计量单",
            PrintTime = new DateTime(2025, 10, 31, 5, 22, 56),
            SerialNumber = "A202510310006",
            MeasurementUnit = "公斤",
            VehicleNumber = "浙A8V676",
            GoodsName = "装修垃圾",
            ShippingUnit = "山河锦旭府",
            ReceivingUnit = "固废资源化综合体",
            EntryTime = new DateTime(2025, 10, 31, 5, 18, 19),
            ExitTime = new DateTime(2025, 10, 31, 5, 22, 56),
            GrossWeight = 16030,
            TareWeight = 7610,
            NetWeight = 8420,
            Type = "村、社区",
            Remarks = "补单",
            ManifestNumber = "",
            TownStreet = "闻堰街道",
            WeigherSignature = "[签字]",
            DriverSignature = "[签字]",
            SupervisorSignature = ""
        };
    }
}
