using System.Text.Json;
using System.Text.Json.Serialization;
using OpenThermalPrintAgent.Core.Models;

namespace OpenThermalPrintAgent.Host.Json;

public sealed class PaperWidthJsonConverter : JsonConverter<PaperWidth>
{
    public override PaperWidth Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return value?.Trim().ToLowerInvariant() switch
        {
            "58mm" => PaperWidth.Mm58,
            "80mm" => PaperWidth.Mm80,
            _ => (PaperWidth)(-1)
        };
    }

    public override void Write(Utf8JsonWriter writer, PaperWidth value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value switch
        {
            PaperWidth.Mm58 => "58mm",
            PaperWidth.Mm80 => "80mm",
            _ => value.ToString()
        });
    }
}
