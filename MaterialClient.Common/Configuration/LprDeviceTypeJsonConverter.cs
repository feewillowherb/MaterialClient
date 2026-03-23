using System.Text.Json;
using System.Text.Json.Serialization;
using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Common.Configuration;

/// <summary>
///     反序列化时兼容旧枚举名 <c>LprAllInOne</c> 与旧文档中的字符串写法。
/// </summary>
public sealed class LprDeviceTypeJsonConverter : JsonConverter<LprDeviceType>
{
    public override LprDeviceType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var n))
            return (LprDeviceType)n;

        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString();
            if (string.IsNullOrEmpty(s))
                return LprDeviceType.Hikvision;

            if (string.Equals(s, "LprAllInOne", StringComparison.OrdinalIgnoreCase))
                return LprDeviceType.Vzvision;

            return Enum.TryParse<LprDeviceType>(s, ignoreCase: true, out var parsed)
                ? parsed
                : LprDeviceType.Hikvision;
        }

        throw new JsonException($"Unexpected token for LprDeviceType: {reader.TokenType}");
    }

    public override void Write(Utf8JsonWriter writer, LprDeviceType value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue((int)value);
    }
}
