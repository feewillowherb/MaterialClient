using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Services.TruckScale.Protocols;
using MaterialClient.Common.Services.TruckScale.Protocols.DingSong;
using MaterialClient.Common.Services.TruckScale.Protocols.DingSongAddr4;
using MaterialClient.Common.Services.TruckScale.Protocols.PortableXpsy;
using MaterialClient.Common.Services.TruckScale.Protocols.TestMode;
using MaterialClient.Common.Services.TruckScale.Protocols.Unsupported;
using MaterialClient.Common.Services.TruckScale.Protocols.Yaohua;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Common.Services.TruckScale.Routing;

public sealed class TruckScaleProtocolRouter : ITruckScaleProtocolRouter, ISingletonDependency
{
    private static readonly UnsupportedTransmissionFormatProtocol Unsupported = new();

    public IScaleTransmissionProtocol Resolve(ScaleType scaleType, TransmissionFormatType format)
    {
        if (format == TransmissionFormatType.TransmissionFormatType1)
            return Unsupported;

        return scaleType switch
        {
            ScaleType.Yaohua => new YaohuaTf0Protocol(),
            ScaleType.DingSong => new DingSongTf0Protocol(),
            ScaleType.DingSongAddr4 => new DingSongAddr4Tf0Protocol(),
            ScaleType.PortableXPSY => new PortableXpsyTf0Protocol(),
            ScaleType.TestMode => new TestModeTf0Protocol(),
            _ => Unsupported,
        };
    }
}
