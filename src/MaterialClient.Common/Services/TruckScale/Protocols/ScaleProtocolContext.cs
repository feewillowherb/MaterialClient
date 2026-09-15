using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Services.Hardware;
using Microsoft.Extensions.Logging;

namespace MaterialClient.Common.Services.TruckScale.Protocols;

/// <summary>
///     Runtime context passed to transmission protocols (serial I/O owned by facade).
/// </summary>
public sealed record ScaleProtocolContext(
    ScaleSettings Settings,
    Func<ISerialPort?> GetSerialPort,
    Action<decimal> PublishWeight,
    Func<decimal, decimal> ConvertWeight,
    ILogger? Logger);
