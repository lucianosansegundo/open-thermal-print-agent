using OpenThermalPrintAgent.Core.Models;

namespace OpenThermalPrintAgent.Core.Printing;

public interface IPrinterProvider
{
    IReadOnlyList<PrinterInfo> ListPrinters();

    bool PrinterExists(string printerName);
}
