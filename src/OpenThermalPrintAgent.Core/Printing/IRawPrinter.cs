namespace OpenThermalPrintAgent.Core.Printing;

public interface IRawPrinter
{
    void PrintRaw(string printerName, ReadOnlySpan<byte> bytes);
}
