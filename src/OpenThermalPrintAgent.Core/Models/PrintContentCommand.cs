namespace OpenThermalPrintAgent.Core.Models;

public sealed record PrintContentCommand
{
    public PrintCommandType Type { get; init; }

    public string? Value { get; init; }

    public string? Data { get; init; }

    public TextAlignment? Align { get; init; }

    public bool? Bold { get; init; }

    public int? Lines { get; init; }

    public int? Size { get; init; }

    public CutMode? Mode { get; init; }

    public BarcodeType? BarcodeType { get; init; }

    public int? WidthBytes { get; init; }

    public int? HeightDots { get; init; }
}
