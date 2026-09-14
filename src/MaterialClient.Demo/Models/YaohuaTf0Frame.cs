using System.Globalization;
using System.Text;

namespace MaterialClient.Demo.Models;

public sealed record YaohuaTf0Frame(decimal DisplayedWeight, int DecimalPlaces, string RawHex)
{
    public const int FrameLength = 12;

    public string FormatDisplayedWeight() =>
        DisplayedWeight.ToString($"F{DecimalPlaces}", CultureInfo.InvariantCulture);

    public static YaohuaTf0Frame? TryParse(ReadOnlySpan<byte> frame)
    {
        if (frame.Length != FrameLength) return null;
        if (frame[0] != 0x02 || frame[FrameLength - 1] != 0x03) return null;
        if (frame[1] is not (0x2B or 0x2D)) return null;

        var digits = 0;
        for (var i = 2; i <= 7; i++)
        {
            var digit = frame[i] - (byte)'0';
            if (digit is < 0 or > 9) return null;
            digits = (digits * 10) + digit;
        }

        var decimalPlaces = frame[8] - (byte)'0';
        if (decimalPlaces is < 0 or > 4) return null;

        var checksum = 0;
        for (var i = 1; i <= 8; i++)
            checksum ^= frame[i];

        if (frame[9] != ToAsciiNibble(checksum >> 4) || frame[10] != ToAsciiNibble(checksum & 0x0F))
            return null;

        decimal weight = digits;
        for (var i = 0; i < decimalPlaces; i++)
            weight /= 10m;

        if (frame[1] == 0x2D)
            weight = -weight;

        return new YaohuaTf0Frame(weight, decimalPlaces, FormatHex(frame));
    }

    public static string FormatDifference(YaohuaTf0Frame minuend, YaohuaTf0Frame subtrahend)
    {
        var places = Math.Max(minuend.DecimalPlaces, subtrahend.DecimalPlaces);
        var value = minuend.DisplayedWeight - subtrahend.DisplayedWeight;
        return value.ToString($"F{places}", CultureInfo.InvariantCulture);
    }

    public string DescribeChecksum()
    {
        var bytes = ParseHex(RawHex);
        if (bytes.Length != FrameLength) return "checksum unavailable";

        var xor = 0;
        for (var i = 1; i <= 8; i++)
            xor ^= bytes[i];

        var expectedHi = ToAsciiNibble(xor >> 4);
        var expectedLo = ToAsciiNibble(xor & 0x0F);
        var match = bytes[9] == expectedHi && bytes[10] == expectedLo;
        return
            $"b10={bytes[9]:X2} b11={bytes[10]:X2} xor={xor:X2} {(match ? "match" : "MISMATCH")}";
    }

    public static string CompareSamples(string beforeHex, string afterHex)
    {
        var before = ParseHex(beforeHex);
        var after = ParseHex(afterHex);
        if (before.Length != FrameLength || after.Length != FrameLength)
            return "sample length is not 12";

        var changed = new StringBuilder();
        for (var i = 0; i < FrameLength; i++)
        {
            if (before[i] == after[i]) continue;
            if (changed.Length > 0) changed.Append(' ');
            changed.Append('b');
            changed.Append(i + 1);
            changed.Append(' ');
            changed.Append(before[i].ToString("X2", CultureInfo.InvariantCulture));
            changed.Append("->");
            changed.Append(after[i].ToString("X2", CultureInfo.InvariantCulture));
        }

        var beforeFrame = TryParse(before);
        var afterFrame = TryParse(after);
        var summary = new StringBuilder();
        summary.Append("before ");
        summary.Append(beforeHex);
        summary.AppendLine();
        summary.Append(beforeFrame?.DescribeChecksum() ?? "before rejected");
        summary.AppendLine();
        summary.Append("after ");
        summary.Append(afterHex);
        summary.AppendLine();
        summary.Append(afterFrame?.DescribeChecksum() ?? "after rejected");
        summary.AppendLine();
        summary.Append(changed.Length == 0 ? "no byte changed" : $"changed {changed}");
        summary.AppendLine();
        if (beforeFrame is not null && afterFrame is not null)
        {
            summary.Append("weight ");
            summary.Append(beforeFrame.FormatDisplayedWeight());
            summary.Append(" -> ");
            summary.Append(afterFrame.FormatDisplayedWeight());
        }

        return summary.ToString();
    }

    private static byte[] ParseHex(string hex)
    {
        var compact = new StringBuilder(hex.Length);
        foreach (var c in hex)
        {
            if (Uri.IsHexDigit(c)) compact.Append(c);
        }

        if (compact.Length < 2 || compact.Length % 2 == 1) return [];
        var bytes = new byte[compact.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
            bytes[i] = Convert.ToByte(compact.ToString(i * 2, 2), 16);
        return bytes;
    }

    public static string FormatHex(ReadOnlySpan<byte> frame)
    {
        var text = new StringBuilder(frame.Length * 3);
        for (var i = 0; i < frame.Length; i++)
        {
            if (i > 0) text.Append(' ');
            text.Append(frame[i].ToString("X2", CultureInfo.InvariantCulture));
        }

        return text.ToString();
    }

    private static byte ToAsciiNibble(int nibble) =>
        (byte)(nibble <= 9 ? nibble + 0x30 : nibble + 0x37);
}

public sealed record YaohuaTf0HexScan(IReadOnlyList<YaohuaTf0Frame> Frames, int DiscardedCount)
{
    public static YaohuaTf0HexScan FromHex(string? text)
    {
        var hex = new StringBuilder();
        if (text is not null)
        {
            foreach (var c in text)
            {
                if (Uri.IsHexDigit(c))
                    hex.Append(c);
            }
        }

        var discarded = 0;
        if (hex.Length % 2 == 1)
        {
            discarded++;
            hex.Length--;
        }

        var bytes = new byte[hex.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
            bytes[i] = Convert.ToByte(hex.ToString(i * 2, 2), 16);

        var frames = new List<YaohuaTf0Frame>();
        var index = 0;
        while (index < bytes.Length)
        {
            if (bytes[index] != 0x02)
            {
                index++;
                continue;
            }

            if (index + YaohuaTf0Frame.FrameLength > bytes.Length)
            {
                discarded++;
                break;
            }

            var parsed = YaohuaTf0Frame.TryParse(bytes.AsSpan(index, YaohuaTf0Frame.FrameLength));
            if (parsed is null)
            {
                discarded++;
                index++;
                continue;
            }

            frames.Add(parsed);
            index += YaohuaTf0Frame.FrameLength;
        }

        return new YaohuaTf0HexScan(frames, discarded);
    }
}
