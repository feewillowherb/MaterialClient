namespace MaterialClient.Common.Configuration;

/// <summary>
///     Plate color priority configuration (from appsettings.json)
/// </summary>
public class PlateColorFilterConfig
{
    /// <summary>
    ///     Plate colors to treat as low-priority (values map to VzvisionColorType)
    /// </summary>
    public List<int> LowPriorityPlateColors { get; set; } = new();
}

