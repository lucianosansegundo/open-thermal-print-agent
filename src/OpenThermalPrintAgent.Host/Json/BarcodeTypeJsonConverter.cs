using System.Text.Json;
using System.Text.Json.Serialization;
using OpenThermalPrintAgent.Core.Models;

namespace OpenThermalPrintAgent.Host.Json;

public sealed class BarcodeTypeJsonConverter : JsonConverter<BarcodeType>
{
    public override BarcodeType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return value?.Trim().ToLowerInvariant() switch
        {
            "code128" or "code-128" => BarcodeType.Code128,
            _ => (BarcodeType)(-1)
        };
    }

    public override void Write(Utf8JsonWriter writer, BarcodeType value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value switch
        {
            BarcodeType.Code128 => "code128",
            _ => value.ToString()
        });
    }
}
