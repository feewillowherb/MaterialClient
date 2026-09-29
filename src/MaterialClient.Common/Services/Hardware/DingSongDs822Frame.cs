namespace MaterialClient.Common.Services.Hardware;

/// <summary>
///     DS822-X continuous frame for <see cref="Entities.Enums.ScaleType.DingSong"/>.
///     Checksum: XOR of all bytes before CHK, then OR with 0x40.
/// </summary>
public sealed record DingSongDs822Frame(decimal WeightKg, char Address, char Command)
{
    public const int FrameLengthA = 22;
    public const int FrameLengthC = 17;

    /// <summary>
    ///     If the span starts with STX and has a command byte, returns the expected full frame length; otherwise null.
    /// </summary>
    public static int? TryGetExpectedLength(ReadOnlySpan<byte> bufferFromStx)
    {
        if (bufferFromStx.Length < 3 || bufferFromStx[0] != 0x02) return null;
        if (bufferFromStx[1] is < (byte)'A' or > (byte)'Z') return null;
        return char.ToUpperInvariant((char)bufferFromStx[2]) switch
        {
            'A' => FrameLengthA,
            'C' => FrameLengthC,
            _ => null
        };
    }

    public static DingSongDs822Frame? TryParse(ReadOnlySpan<byte> frame)
    {
        if (frame.Length is not (FrameLengthA or FrameLengthC)) return null;
        if (frame[0] != 0x02 || frame[^1] != 0x03) return null;
        if (frame[1] is < (byte)'A' or > (byte)'Z') return null;

        var expectedLength = TryGetExpectedLength(frame);
        if (expectedLength is null || frame.Length != expectedLength.Value) return null;

        var chkIndex = frame.Length - 2;
        byte xor = 0;
        for (var i = 0; i < chkIndex; i++)
            xor ^= frame[i];
        if (frame[chkIndex] != (byte)(xor | 0x40)) return null;

        var address = (char)frame[1];
        var command = (char)frame[2];
        return char.ToUpperInvariant(command) switch
        {
            'A' => TryParseA(frame, address, command),
            'C' => TryParseC(frame, address, command),
            _ => null
        };
    }

    private static DingSongDs822Frame? TryParseA(ReadOnlySpan<byte> frame, char address, char command)
    {
        if (frame[3] is not ((byte)'+' or (byte)'-')) return null;

        var digits = 0;
        for (var i = 4; i <= 9; i++)
        {
            var digit = frame[i] - (byte)'0';
            if (digit is < 0 or > 9) return null;
            digits = (digits * 10) + digit;
        }

        var decimalPlaces = frame[10] - (byte)'0';
        if (decimalPlaces is < 0 or > 4) return null;

        for (var i = 11; i <= 16; i++)
        {
            var digit = frame[i] - (byte)'0';
            if (digit is < 0 or > 9) return null;
        }

        decimal weight = digits;
        for (var i = 0; i < decimalPlaces; i++)
            weight /= 10m;
        if (frame[3] == (byte)'-')
            weight = -weight;

        return new DingSongDs822Frame(weight, address, command);
    }

    private static DingSongDs822Frame? TryParseC(ReadOnlySpan<byte> frame, char address, char command)
    {
        // Six (hi, lo) pairs at bytes 3–14; digit from hi when ASCII 0–9; lo ignored.
        var digits = 0;
        var hasDigit = false;
        for (var pair = 0; pair < 6; pair++)
        {
            var hi = frame[3 + pair * 2];
            if (hi is < (byte)'0' or > (byte)'9')
                continue;
            hasDigit = true;
            digits = (digits * 10) + (hi - (byte)'0');
        }

        if (!hasDigit) return null;
        return new DingSongDs822Frame(digits, address, command);
    }

    /// <summary>
    ///     Build a valid Adr=1 continuous frame for tests (tare/status zeroed).
    /// </summary>
    public static byte[] BuildAFrameForTests(
        char address,
        char command,
        char sign,
        string sixDigits,
        int decimalPlaces,
        string sixTareDigits = "000000")
    {
        if (sixDigits.Length != 6) throw new ArgumentException("Need 6 weight digits.", nameof(sixDigits));
        if (sixTareDigits.Length != 6) throw new ArgumentException("Need 6 tare digits.", nameof(sixTareDigits));
        if (decimalPlaces is < 0 or > 4) throw new ArgumentOutOfRangeException(nameof(decimalPlaces));

        var frame = new byte[FrameLengthA];
        frame[0] = 0x02;
        frame[1] = (byte)address;
        frame[2] = (byte)command;
        frame[3] = (byte)sign;
        for (var i = 0; i < 6; i++)
            frame[4 + i] = (byte)sixDigits[i];
        frame[10] = (byte)('0' + decimalPlaces);
        for (var i = 0; i < 6; i++)
            frame[11 + i] = (byte)sixTareDigits[i];
        frame[17] = (byte)'0'; // e
        frame[18] = (byte)'0'; // ff
        frame[19] = (byte)'0';
        byte xor = 0;
        for (var i = 0; i < FrameLengthA - 2; i++)
            xor ^= frame[i];
        frame[20] = (byte)(xor | 0x40);
        frame[21] = 0x03;
        return frame;
    }
}
