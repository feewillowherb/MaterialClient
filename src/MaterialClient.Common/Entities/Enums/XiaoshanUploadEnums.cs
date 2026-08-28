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
    [System.ComponentModel.Description("进")]
    Enter = 0,
    [System.ComponentModel.Description("出")]
    Exit = 1
}

[JsonConverter(typeof(JsonStringEnumConverter<UrbanSiteType>))]
public enum UrbanSiteType
{
    [System.ComponentModel.Description("工地")]
    Construction = 0,
    [System.ComponentModel.Description("消纳")]
    Disposal = 1
}
