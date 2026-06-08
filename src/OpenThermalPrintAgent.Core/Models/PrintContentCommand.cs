namespace OpenThermalPrintAgent.Core.Models;

public sealed record PrintContentCommand
{
    public PrintCommandType Type { get; init; }

    public string? Value { get; init; }

    public TextAlignment? Align { get; init; }

    public bool? Bold { get; init; }

    public int? Lines { get; init; }
}
