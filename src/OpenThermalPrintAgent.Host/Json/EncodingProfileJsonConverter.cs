using System.Text.Json;
using System.Text.Json.Serialization;
using OpenThermalPrintAgent.Core.Models;

namespace OpenThermalPrintAgent.Host.Json;

public sealed class EncodingProfileJsonConverter : JsonConverter<EncodingProfile>
{
    public override EncodingProfile Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return value?.Trim().ToLowerInvariant() switch
        {
            "latin1" or "latin-1" or "iso-8859-1" => EncodingProfile.Latin1,
            "cp850" or "ibm850" or "850" => EncodingProfile.Cp850,
            "cp858" or "ibm858" or "858" => EncodingProfile.Cp858,
            _ => (EncodingProfile)(-1)
        };
    }

    public override void Write(Utf8JsonWriter writer, EncodingProfile value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value switch
        {
            EncodingProfile.Latin1 => "latin1",
            EncodingProfile.Cp850 => "cp850",
            EncodingProfile.Cp858 => "cp858",
            _ => value.ToString()
        });
    }
}
