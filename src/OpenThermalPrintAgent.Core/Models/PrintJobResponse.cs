namespace OpenThermalPrintAgent.Core.Models;

public sealed record PrintJobResponse
{
    public required string JobId { get; init; }

    public required string Status { get; init; }

    public required string PrinterName { get; init; }

    public required DateTimeOffset PrintedAt { get; init; }
}
