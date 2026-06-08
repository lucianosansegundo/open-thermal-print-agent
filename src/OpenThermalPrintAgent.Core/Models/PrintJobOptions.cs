namespace OpenThermalPrintAgent.Core.Models;

public sealed record PrintJobOptions
{
    public bool Cut { get; init; }

    public bool OpenDrawer { get; init; }

    public int Copies { get; init; } = 1;
}
