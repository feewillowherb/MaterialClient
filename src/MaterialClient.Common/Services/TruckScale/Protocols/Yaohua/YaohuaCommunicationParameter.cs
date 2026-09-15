using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Common.Services.TruckScale.Protocols.Yaohua;

/// <summary>
///     Yaohua-owned parse of opaque <c>CommunicationParameter</c> as address A–Z.
/// </summary>
public static class YaohuaCommunicationParameter
{
    public static char ResolveAddressOrThrow(string? parameter)
    {
        if (string.IsNullOrWhiteSpace(parameter))
            return 'A';

        var trimmed = parameter.Trim();
        if (trimmed.Length == 0)
            return 'A';

        var c = char.ToUpperInvariant(trimmed[0]);
        if (c is < 'A' or > 'Z')
            throw new ArgumentException(
                $"Yaohua CommunicationParameter must be address A-Z, got '{parameter}'.",
                nameof(parameter));

        return c;
    }

    public static bool TryResolveAddress(string? parameter, out char address)
    {
        try
        {
            address = ResolveAddressOrThrow(parameter);
            return true;
        }
        catch (ArgumentException)
        {
            address = 'A';
            return false;
        }
    }
}
