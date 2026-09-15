using System.Globalization;
using System.Text;
using MaterialClient.Common.Entities.Enums;
using Microsoft.Extensions.Logging;

namespace MaterialClient.Common.Services.TruckScale.Protocols.PortableXpsy;

public sealed class PortableXpsyTf0Protocol : IScaleTransmissionProtocol
{
    private const int FrameLength = 9;
    private const int PayloadLength = 8;

    private readonly byte[] _buffer = new byte[4096];
    private int _bufferIndex;

    public bool Supports(ScaleType scaleType, TransmissionFormatType format) =>
        scaleType == ScaleType.PortableXPSY &&
        format == TransmissionFormatType.TransmissionFormatType0;

    public void EnsureSupported(ScaleType scaleType, TransmissionFormatType format)
    {
        if (!Supports(scaleType, format))
            throw new UnsupportedTransmissionFormatException(scaleType, format);
    }

    public void OnStart(ScaleProtocolContext context)
    {
        _bufferIndex = 0;
    }

    public void OnDataReceived(ScaleProtocolContext context)
    {
        try
        {
            var port = context.GetSerialPort();
            if (port == null || !port.IsOpen) return;

            var availableBytes = port.BytesToRead;
            if (availableBytes <= 0) return;

            if (_bufferIndex + availableBytes > _buffer.Length)
            {
                context.Logger?.LogWarning("Portable XP-SY receive buffer overflow, resetting");
                _bufferIndex = 0;
            }

            var bytesRead = port.Read(_buffer, _bufferIndex, availableBytes);
            if (bytesRead <= 0) return;

            _bufferIndex += bytesRead;

            while (_bufferIndex >= FrameLength)
            {
                var startIndex = FindFrameStart();
                if (startIndex < 0)
                {
                    if (_bufferIndex > PayloadLength)
                    {
                        Array.Copy(
                            _buffer,
                            _bufferIndex - PayloadLength,
                            _buffer,
                            0,
                            PayloadLength);
                        _bufferIndex = PayloadLength;
                    }

                    return;
                }

                if (startIndex > 0)
                {
                    Array.Copy(_buffer, startIndex, _buffer, 0, _bufferIndex - startIndex);
                    _bufferIndex -= startIndex;
                }

                if (_bufferIndex < FrameLength) return;

                var message = new byte[FrameLength];
                Array.Copy(_buffer, 0, message, 0, FrameLength);

                var parsedWeight = TryParseMessage(message, context.Logger);
                if (parsedWeight.HasValue)
                {
                    var convertedWeight = context.ConvertWeight(parsedWeight.Value);
                    context.PublishWeight(convertedWeight);
                }

                Array.Copy(
                    _buffer,
                    FrameLength,
                    _buffer,
                    0,
                    _bufferIndex - FrameLength);
                _bufferIndex -= FrameLength;
            }
        }
        catch (Exception ex)
        {
            context.Logger?.LogWarning(ex, "Error receiving Portable XP-SY data from truck scale");
            _bufferIndex = 0;
        }
    }

    public void OnStop()
    {
        _bufferIndex = 0;
    }

    private int FindFrameStart()
    {
        for (var i = 0; i <= _bufferIndex - FrameLength; i++)
        {
            if (_buffer[i + PayloadLength] != (byte)'=')
                continue;

            if (IsValidPayload(_buffer, i))
                return i;
        }

        return -1;
    }

    private static bool IsValidPayload(byte[] buffer, int offset)
    {
        var hasDigit = false;
        for (var j = 0; j < PayloadLength; j++)
        {
            var b = buffer[offset + j];
            if (b is >= (byte)'0' and <= (byte)'9')
            {
                hasDigit = true;
                continue;
            }

            if (b is (byte)'.' or (byte)'-')
                continue;

            return false;
        }

        return hasDigit;
    }

    internal static decimal? TryParseMessage(byte[] message, ILogger? logger)
    {
        try
        {
            if (message.Length != FrameLength)
                return null;

            if (message[PayloadLength] != (byte)'=')
                return null;

            if (!IsValidPayload(message, 0))
                return null;

            var payload = Encoding.ASCII.GetString(message, 0, PayloadLength);
            var reversed = payload.ToCharArray();
            Array.Reverse(reversed);
            var weightText = new string(reversed).Trim();

            if (!decimal.TryParse(
                    weightText,
                    NumberStyles.Float | NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture,
                    out var weight))
            {
                logger?.LogWarning("Failed to parse Portable XP-SY weight: {WeightText}", weightText);
                return null;
            }

            logger?.LogDebug(
                "Parsed Portable XP-SY weight: {Weight} (payload: {Payload}, reversed: {WeightText})",
                weight,
                payload,
                weightText);
            return weight;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Error parsing Portable XP-SY weight data");
            return null;
        }
    }
}
