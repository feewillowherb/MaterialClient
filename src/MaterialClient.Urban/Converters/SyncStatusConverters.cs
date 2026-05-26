using System.Globalization;
using Avalonia.Data.Converters;
using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Urban.Converters;

/// <summary>
///     Converts a <see cref="SyncStatus" /> value to a boolean for status badge visibility.
///     When <see cref="Invert" /> is <c>false</c> (default), returns <c>true</c> when status matches <see cref="TargetStatus" />.
///     When <see cref="Invert" /> is <c>true</c>, returns <c>true</c> when status does NOT match.
/// </summary>
public class SyncStatusMatchConverter : IValueConverter
{
    /// <summary>
    ///     The SyncStatus value to compare against.
    /// </summary>
    public SyncStatus TargetStatus { get; set; } = SyncStatus.Failed;

    /// <summary>
    ///     When <c>true</c>, inverts the match result (returns true when status != TargetStatus).
    /// </summary>
    public bool Invert { get; set; } = false;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is SyncStatus status)
            return Invert ? status != TargetStatus : status == TargetStatus;

        // If UrbanExtension is null, the binding produces null — hide the badge
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
///     Inverts a boolean value for visibility bindings.
///     Used to show elements when a bound boolean is <c>false</c>.
/// </summary>
public class BoolInvertConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
            return !b;

        // If value is null (e.g., UrbanExtension is null), hide the badge
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
