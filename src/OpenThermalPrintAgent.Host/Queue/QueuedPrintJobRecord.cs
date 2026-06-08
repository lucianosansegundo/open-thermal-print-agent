using OpenThermalPrintAgent.Core.Models;

namespace OpenThermalPrintAgent.Host.Queue;

public sealed record QueuedPrintJobRecord
{
    public required string JobId { get; init; }

    public required string PrinterName { get; init; }

    public required string Status { get; init; }

    public int Attempts { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public PrintJobRequest? Request { get; init; }
}
