using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Services.TruckScale.Protocols;

namespace MaterialClient.Common.Services.TruckScale.Routing;

public interface ITruckScaleProtocolRouter
{
    IScaleTransmissionProtocol Resolve(ScaleType scaleType, TransmissionFormatType format);
}
