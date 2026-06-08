using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Management;
using OpenThermalPrintAgent.Core.Models;
using OpenThermalPrintAgent.Core.Printing;

namespace OpenThermalPrintAgent.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsPrinterProvider : IPrinterProvider
{
    public IReadOnlyList<PrinterInfo> ListPrinters()
    {
        EnsureWindows();

        using var searcher = new ManagementObjectSearcher(
            "SELECT Name, DriverName, PortName, Default, PrinterStatus, DetectedErrorState, WorkOffline FROM Win32_Printer");

        return searcher.Get()
            .OfType<ManagementObject>()
            .Select(CreatePrinterInfo)
            .OrderBy(printer => printer.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public bool PrinterExists(string printerName)
    {
        if (string.IsNullOrWhiteSpace(printerName))
        {
            return false;
        }

        return ListPrinters().Any(printer => string.Equals(printer.Name, printerName, StringComparison.OrdinalIgnoreCase));
    }

    private static PrinterInfo CreatePrinterInfo(ManagementBaseObject printer)
    {
        var printerStatus = GetUInt16(printer, "PrinterStatus");
        var detectedErrorState = GetUInt16(printer, "DetectedErrorState");
        var workOffline = GetBoolean(printer, "WorkOffline");
        var status = WindowsPrinterStatusMapper.MapStatus(printerStatus, detectedErrorState, workOffline);

        return new PrinterInfo
        {
            Name = GetString(printer, "Name") ?? string.Empty,
            IsDefault = GetBoolean(printer, "Default") ?? false,
            DriverName = GetString(printer, "DriverName"),
            PortName = GetString(printer, "PortName"),
            Status = status,
            IsOnline = WindowsPrinterStatusMapper.IsOnline(status),
            WorkOffline = workOffline,
            Capabilities = ["raw", "escpos"]
        };
    }

    private static string? GetString(ManagementBaseObject printer, string propertyName) =>
        printer[propertyName] as string;

    private static bool? GetBoolean(ManagementBaseObject printer, string propertyName) =>
        printer[propertyName] as bool?;

    private static ushort? GetUInt16(ManagementBaseObject printer, string propertyName) =>
        printer[propertyName] as ushort?;

    private static void EnsureWindows()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException("The Windows printer provider requires Windows.");
        }
    }
}
