using System.Text.Json;
using System.Text.Json.Serialization;
using OpenThermalPrintAgent.Core.Models;

namespace OpenThermalPrintAgent.Host.Json;

public sealed class PrintCommandTypeJsonConverter : JsonConverter<PrintCommandType>
{
    public override PrintCommandType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return value?.Trim().ToLowerInvariant() switch
        {
            "text" => PrintCommandType.Text,
            "feed" => PrintCommandType.Feed,
            "cut" => PrintCommandType.Cut,
            "opendrawer" or "openDrawer" or "drawer" => PrintCommandType.OpenDrawer,
            "qrcode" or "qrCode" or "qr" => PrintCommandType.QrCode,
            "barcode" => PrintCommandType.Barcode,
            "image" or "rasterImage" => PrintCommandType.Image,
            _ => (PrintCommandType)(-1)
        };
    }

    public override void Write(Utf8JsonWriter writer, PrintCommandType value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value switch
        {
            PrintCommandType.Text => "text",
            PrintCommandType.Feed => "feed",
            PrintCommandType.Cut => "cut",
            PrintCommandType.OpenDrawer => "openDrawer",
            PrintCommandType.QrCode => "qrCode",
            PrintCommandType.Barcode => "barcode",
            PrintCommandType.Image => "image",
            _ => value.ToString()
        });
    }
}
