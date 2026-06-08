namespace OpenThermalPrintAgent.Core.Models;

public sealed record TestPrintRequest
{
    public string PrinterName { get; init; } = string.Empty;

    public PaperWidth PaperWidth { get; init; } = PaperWidth.Mm80;

    public bool Cut { get; init; } = true;

    public bool OpenDrawer { get; init; }
}
