using System.Text.RegularExpressions;
using OpenThermalPrintAgent.Core.Models;

namespace OpenThermalPrintAgent.Windows;

public static class WindowsPrinterClassifier
{
    public static PrinterInfo Classify(PrinterInfo printer, ushort? markingTechnology = null)
    {
        var identity = $"{printer.Name} {printer.DriverName}";
        var isVirtual = Regex.IsMatch(identity,
            @"\b(PDF|AdobePDF|PDFCreator|CutePDF|doPDF|XPS|Fax|OneNote)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            || string.Equals(printer.PortName, "PORTPROMPT:", StringComparison.OrdinalIgnoreCase);
        var classification = "unknown";
        if (isVirtual)
            classification = "notThermal";
        else if (markingTechnology is >= 16 and <= 19)
            classification = "likelyThermal";
        else if (markingTechnology is >= 3 and <= 27)
            classification = "notThermal";
        else if (Regex.IsMatch(identity,
            @"\b(EPSON\s+TM[- ]?(T20|T70|T82|T88|T90|m10|m30)[a-z]*|Star\s+TSP(100|143|650|654|700|743)[a-z]*|Citizen\s+CT[- ]S(310|601|651|801|851)[a-z]*|XP-80C?)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            classification = "likelyThermal";

        return printer with { IsVirtual = isVirtual ? true : null, ThermalClassification = classification };
    }
}
