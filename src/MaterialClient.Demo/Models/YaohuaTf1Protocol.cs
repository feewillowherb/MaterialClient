using System.Globalization;
using System.Text;

namespace MaterialClient.Demo.Models;

public sealed record YaohuaTf1Command(char Address, char Command, byte[] Bytes, string RawHex);

public sealed record YaohuaTf1Weight(decimal DisplayedWeight, int DecimalPlaces, string RawHex)
{
    public string FormatDisplayedWeight() =>
        DisplayedWeight.ToString($"F{DecimalPlaces}", CultureInfo.InvariantCulture);
}

public sealed record YaohuaTf1Reply(
    char Address,
    char Command,
    YaohuaTf1Weight? Weight,
    string? TextPayload,
    string RawHex,
    bool ChecksumMatch);

public static class YaohuaTf1Protocol
{
    public static YaohuaTf1Command Build(char address, char command)
    {
        var xor = address ^ command;
        var bytes = new byte[]
        {
            0x02,
            (byte)address,
            (byte)command,
            ToAsciiNibble(xor >> 4),
            ToAsciiNibble(xor & 0x0F),
            0x03
        };
        return new YaohuaTf1Command(address, command, bytes, YaohuaTf0Frame.FormatHex(bytes));
    }

    public static YaohuaTf1Reply? TryParse(ReadOnlySpan<byte> buffer)
    {
        var start = buffer.IndexOf((byte)0x02);
        if (start < 0) return null;
        var end = buffer[start..].IndexOf((byte)0x03);
        if (end < 0) return null;
        var frame = buffer.Slice(start, end + 1);
        if (frame.Length < 6) return null;

        var xor = 0;
        for (var i = 1; i < frame.Length - 3; i++)
            xor ^= frame[i];

        var match = frame[^3] == ToAsciiNibble(xor >> 4) && frame[^2] == ToAsciiNibble(xor & 0x0F);
        var address = (char)frame[1];
        var command = (char)frame[2];
        YaohuaTf1Weight? weight = null;
        string? textPayload = null;

        if (frame.Length == 14 && command is 'B' or 'C' or 'D')
            weight = TryParseWeight(frame);
        else if (command is 'E' or 'F')
            textPayload = TryParseTextPayload(frame);

        return new YaohuaTf1Reply(address, command, weight, textPayload, YaohuaTf0Frame.FormatHex(frame), match);
    }

    private static YaohuaTf1Weight? TryParseWeight(ReadOnlySpan<byte> frame)
    {
        if (frame[3] is not (0x2B or 0x2D)) return null;

        var digits = 0;
        for (var i = 4; i <= 9; i++)
        {
            var digit = frame[i] - (byte)'0';
            if (digit is < 0 or > 9) return null;
            digits = (digits * 10) + digit;
        }

        var decimalPlaces = frame[10] - (byte)'0';
        if (decimalPlaces is < 0 or > 4) return null;

        decimal weight = digits;
        for (var i = 0; i < decimalPlaces; i++)
            weight /= 10m;
        if (frame[3] == 0x2D)
            weight = -weight;

        return new YaohuaTf1Weight(weight, decimalPlaces, YaohuaTf0Frame.FormatHex(frame));
    }

    /// <summary>
    ///     E (vehicle no.): 11-byte frame, 5 ASCII digits after command.
    ///     F (goods no.): 14-byte frame, 8 ASCII digits after command.
    ///     Payload is bytes [3 .. length-4] (before XOR nibbles).
    /// </summary>
    private static string? TryParseTextPayload(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < 7) return null;
        var payload = frame[3..^3];
        if (payload.IsEmpty) return null;

        var sb = new StringBuilder(payload.Length);
        foreach (var b in payload)
        {
            if (b is < 0x20 or > 0x7E)
                return null;
            sb.Append((char)b);
        }

        return sb.ToString().Trim();
    }

    private static byte ToAsciiNibble(int nibble) =>
        (byte)(nibble <= 9 ? nibble + 0x30 : nibble + 0x37);
}
