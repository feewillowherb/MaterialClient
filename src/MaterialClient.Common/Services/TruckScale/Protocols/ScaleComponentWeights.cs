namespace MaterialClient.Common.Services.TruckScale.Protocols;

/// <summary>
///     Instrument-reported tare / gross / net in tons (after unit conversion).
///     <see cref="AllValid"/> is true only when all three values are present.
/// </summary>
public readonly record struct ScaleComponentWeights(
    decimal? GrossTon,
    decimal? TareTon,
    decimal? NetTon)
{
    public bool AllValid =>
        GrossTon.HasValue &&
        TareTon.HasValue &&
        NetTon.HasValue;

    public static ScaleComponentWeights Invalid => default;

    public static ScaleComponentWeights FromGrossTareTons(decimal grossTon, decimal tareTon) =>
        new(grossTon, tareTon, grossTon - tareTon);
}
