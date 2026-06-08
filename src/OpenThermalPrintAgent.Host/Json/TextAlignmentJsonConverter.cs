using System.Text.Json;
using System.Text.Json.Serialization;
using OpenThermalPrintAgent.Core.Models;

namespace OpenThermalPrintAgent.Host.Json;

public sealed class TextAlignmentJsonConverter : JsonConverter<TextAlignment>
{
    public override TextAlignment Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return value?.Trim().ToLowerInvariant() switch
        {
            "left" => TextAlignment.Left,
            "center" => TextAlignment.Center,
            "right" => TextAlignment.Right,
            _ => (TextAlignment)(-1)
        };
    }

    public override void Write(Utf8JsonWriter writer, TextAlignment value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value switch
        {
            TextAlignment.Left => "left",
            TextAlignment.Center => "center",
            TextAlignment.Right => "right",
            _ => value.ToString()
        });
    }
}
