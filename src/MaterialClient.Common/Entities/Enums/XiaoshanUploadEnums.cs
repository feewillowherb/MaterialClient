using System.Text.Json.Serialization;

namespace MaterialClient.Common.Entities.Enums;

[JsonConverter(typeof(JsonStringEnumConverter<XiaoshanUploadMode>))]
public enum XiaoshanUploadMode
{
    Weighbridge,
    Gate,
    Product
}

[JsonConverter(typeof(JsonStringEnumConverter<UrbanInOutType>))]
public enum UrbanInOutType
{
    Enter = 0,
    Exit = 1
}

[JsonConverter(typeof(JsonStringEnumConverter<UrbanSiteType>))]
public enum UrbanSiteType
{
    Construction = 0,
    Disposal = 1
}
