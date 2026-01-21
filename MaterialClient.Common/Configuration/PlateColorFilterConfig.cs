namespace MaterialClient.Common.Configuration;

/// <summary>
///     Plate color filtering configuration (from appsettings.json)
/// </summary>
public class PlateColorFilterConfig
{
    /// <summary>
    ///     Plate colors to filter out (values map to LprAllInOneColorType)
    /// </summary>
    public List<int> FilteredPlateColors { get; set; } = new();
}

