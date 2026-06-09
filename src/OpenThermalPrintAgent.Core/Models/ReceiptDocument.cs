using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenThermalPrintAgent.Core.Models;

public sealed record ReceiptDocument
{
    public string? Title { get; init; }

    public string? Subtitle { get; init; }

    public IReadOnlyList<ReceiptBlock> Blocks { get; init; } = [];
}

public sealed record ReceiptBlock
{
    public string Type { get; init; } = string.Empty;

    public string? Label { get; init; }

    public JsonElement? Lines { get; init; }

    public TextAlignment? Align { get; init; }

    public bool? Bold { get; init; }

    public IReadOnlyList<ReceiptKeyValueRow> Rows { get; init; } = [];

    public IReadOnlyList<ReceiptItem> Items { get; init; } = [];

    [JsonPropertyName("char")]
    public string? SeparatorChar { get; init; }
}

public sealed record ReceiptKeyValueRow
{
    public string Label { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;

    public bool? Bold { get; init; }
}

public sealed record ReceiptItem
{
    public string Name { get; init; } = string.Empty;

    public string? Quantity { get; init; }

    public string? UnitPrice { get; init; }

    public string? Total { get; init; }

    public string? Comment { get; init; }
}
