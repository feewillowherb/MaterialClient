namespace MaterialClient.Common.Entities;

/// <summary>
///     称重记录物料项（作为 JSON 存储在 WeighingRecord 中）
/// </summary>
public class WeighingRecordMaterial
{
    /// <summary>
    ///     构造函数
    /// </summary>
    public WeighingRecordMaterial()
    {
    }

    /// <summary>
    ///     构造函数
    /// </summary>
    public WeighingRecordMaterial(decimal weight, int? materialId, int? materialUnitId, decimal? waybillQuantity)
    {
        Weight = weight;
        MaterialId = materialId;
        MaterialUnitId = materialUnitId;
        WaybillQuantity = waybillQuantity;
    }

    /// <summary>
    ///     重量
    /// </summary>
    public decimal Weight { get; set; }

    /// <summary>
    ///     材料Id
    /// </summary>
    public int? MaterialId { get; set; }

    /// <summary>
    ///     单位Id
    /// </summary>
    public int? MaterialUnitId { get; set; }

    /// <summary>
    ///     运单数量
    /// </summary>
    public decimal? WaybillQuantity { get; set; }
}