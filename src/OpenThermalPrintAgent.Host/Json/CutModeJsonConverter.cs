using System.Text.Json;
using System.Text.Json.Serialization;
using OpenThermalPrintAgent.Core.Models;

namespace OpenThermalPrintAgent.Host.Json;

public sealed class CutModeJsonConverter : JsonConverter<CutMode>
{
    public override CutMode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return value?.Trim().ToLowerInvariant() switch
        {
            "none" => CutMode.None,
            "full" => CutMode.Full,
            "partial" => CutMode.Partial,
            "feedandfull" or "feed_and_full" or "feed-and-full" => CutMode.FeedAndFull,
            "feedandpartial" or "feed_and_partial" or "feed-and-partial" => CutMode.FeedAndPartial,
            _ => (CutMode)(-1)
        };
    }

    public override void Write(Utf8JsonWriter writer, CutMode value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value switch
        {
            CutMode.None => "none",
            CutMode.Full => "full",
            CutMode.Partial => "partial",
            CutMode.FeedAndFull => "feedAndFull",
            CutMode.FeedAndPartial => "feedAndPartial",
            _ => value.ToString()
        });
    }
}
