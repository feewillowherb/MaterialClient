using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

namespace MaterialClient.Demo.Models;

public sealed record ObservedFrame(byte[] Bytes, YaohuaTf0Frame? Weight);

public sealed record FrameScan(YaohuaTf0Frame? LatestWeight, IReadOnlyList<ObservedFrame> Frames);

public sealed class FrameKindRow : INotifyPropertyChanged
{
    private int _count = 1;

    public FrameKindRow(string signature, string exampleHex, int length, string kind)
    {
        Signature = signature;
        ExampleHex = exampleHex;
        Length = length;
        Kind = kind;
    }

    public string Signature { get; }

    public string ExampleHex { get; }

    public int Length { get; }

    public string Kind { get; }

    public int Count
    {
        get => _count;
        set
        {
            if (_count == value) return;
            _count = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public static class YaohuaFrameScanner
{
    public const int MaxFrameLength = 80;
    public const int MinFrameLength = 4;

    public static FrameScan Extract(List<byte> pending)
    {
        var frames = new List<ObservedFrame>();
        YaohuaTf0Frame? latestWeight = null;
        var index = 0;

        while (index < pending.Count)
        {
            if (pending[index] == 0x02)
            {
                var stx = TakeStxFrame(pending, index);
                if (stx.Wait) break;
                if (stx.Bytes is null)
                {
                    index++;
                    continue;
                }

                var weight = YaohuaTf0Frame.TryParse(stx.Bytes);
                if (weight is not null) latestWeight = weight;
                frames.Add(new ObservedFrame(stx.Bytes, weight));
                index += stx.Bytes.Length;
                continue;
            }

            var text = TakeTextFrame(pending, index);
            if (text.Wait) break;
            if (text.Bytes is not null)
            {
                frames.Add(new ObservedFrame(text.Bytes, null));
                index += text.Bytes.Length;
                continue;
            }

            index++;
        }

        if (index > 0)
            pending.RemoveRange(0, index);

        return new FrameScan(latestWeight, frames);
    }

    public static string Signature(ReadOnlySpan<byte> frame)
    {
        var text = new StringBuilder(frame.Length * 2);
        text.Append(frame.Length);
        text.Append(':');
        foreach (var b in frame)
        {
            text.Append(' ');
            if (b is 0x02 or 0x03 or 0x0D or 0x0A or 0x3D or 0x20 or 0x2A)
                text.Append(b.ToString("X2"));
            else if (b is 0x2B or 0x2D)
                text.Append('S');
            else if (b is >= (byte)'0' and <= (byte)'9')
                text.Append('D');
            else if (b is >= (byte)'A' and <= (byte)'F' or >= (byte)'a' and <= (byte)'f')
                text.Append('H');
            else
                text.Append(b.ToString("X2"));
        }

        return text.ToString();
    }

    private static FrameTake TakeStxFrame(List<byte> pending, int start)
    {
        var limit = Math.Min(pending.Count, start + MaxFrameLength);
        for (var i = start + 1; i < limit; i++)
        {
            if (pending[i] != 0x03) continue;
            var length = i - start + 1;
            if (length < MinFrameLength) return FrameTake.Skip;
            return FrameTake.Took(Copy(pending, start, length));
        }

        if (pending.Count - start < MaxFrameLength)
            return FrameTake.Waiting;

        return FrameTake.Skip;
    }

    private static FrameTake TakeTextFrame(List<byte> pending, int start)
    {
        if (!IsPrintable(pending[start])) return FrameTake.Skip;

        var limit = Math.Min(pending.Count, start + MaxFrameLength);
        for (var i = start; i < limit; i++)
        {
            var b = pending[i];
            if (b is 0x3D or 0x0D)
            {
                var length = i - start + 1;
                if (length < MinFrameLength) return FrameTake.Skip;
                return FrameTake.Took(Copy(pending, start, length));
            }

            if (!IsPrintable(b)) return FrameTake.Skip;
        }

        if (pending.Count - start < MaxFrameLength)
            return FrameTake.Waiting;

        return FrameTake.Skip;
    }

    private static bool IsPrintable(byte b) => b is >= 0x20 and <= 0x7E;

    private static byte[] Copy(List<byte> pending, int start, int length)
    {
        var bytes = new byte[length];
        pending.CopyTo(start, bytes, 0, length);
        return bytes;
    }

    private sealed record FrameTake(bool Wait, byte[]? Bytes)
    {
        public static FrameTake Waiting { get; } = new(true, null);

        public static FrameTake Skip { get; } = new(false, null);

        public static FrameTake Took(byte[] bytes) => new(false, bytes);
    }
}
