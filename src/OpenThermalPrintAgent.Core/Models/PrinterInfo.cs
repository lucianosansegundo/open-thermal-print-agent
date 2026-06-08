namespace OpenThermalPrintAgent.Core.Models;

public sealed record PrinterInfo
{
    public required string Name { get; init; }

    public bool IsDefault { get; init; }

    public IReadOnlyList<string> Capabilities { get; init; } = [];
}
