namespace OpenThermalPrintAgent.Core.Models;

public sealed record PrinterInfo
{
    public required string Name { get; init; }

    public bool IsDefault { get; init; }

    public string? DriverName { get; init; }

    public string? PortName { get; init; }

    public string? Status { get; init; }

    public bool? IsOnline { get; init; }

    public bool? WorkOffline { get; init; }

    public IReadOnlyList<string> Capabilities { get; init; } = [];
}
