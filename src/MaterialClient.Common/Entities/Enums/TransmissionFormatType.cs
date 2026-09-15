using System.ComponentModel;

namespace MaterialClient.Common.Entities.Enums;

/// <summary>
///     Instrument transmission format type (maps to Yaohua tF and similar).
/// </summary>
public enum TransmissionFormatType
{
    /// <summary>Continuous send (tF=0).</summary>
    [Description("(tF0)")]
    TransmissionFormatType0 = 0,

    /// <summary>
    ///     Continuous query / listen (product tF1). Yaohua: no host query command writes.
    /// </summary>
    [Description("(tF1)")]
    TransmissionFormatType1 = 1,
}
