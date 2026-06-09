namespace OpenThermalPrintAgent.Core.Models;

public sealed record PrintJobRequest
{
    public string? JobId { get; init; }

    public string PrinterName { get; init; } = string.Empty;

    public string Format { get; init; } = "escpos";

    public PaperWidth PaperWidth { get; init; } = PaperWidth.Mm80;

    public PrintJobOptions Options { get; init; } = new();

    public IReadOnlyList<PrintContentCommand> Content { get; init; } = [];

    public ReceiptDocument? Receipt { get; init; }
}
