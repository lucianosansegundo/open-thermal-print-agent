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

        return Discover(ReadWmiPrinters, WindowsSpoolerDiscovery.ListPrinters);
    }

    internal static IReadOnlyList<PrinterInfo> Discover(
        Func<IReadOnlyList<PrinterInfo>> readWmi, Func<IReadOnlyList<PrinterInfo>> readFallback)
    {
        try { return readWmi(); }
        catch (Exception ex) when (ex is ManagementException or COMException or UnauthorizedAccessException
            or System.Security.SecurityException or InvalidCastException)
        {
            return readFallback();
        }
    }

    private static IReadOnlyList<PrinterInfo> ReadWmiPrinters()
    {
        using var searcher = new ManagementObjectSearcher(
            "SELECT Name, DriverName, PortName, Default, PrinterStatus, DetectedErrorState, WorkOffline, MarkingTechnology FROM Win32_Printer");
        using var results = searcher.Get();
        var printers = new List<PrinterInfo>();
        foreach (ManagementObject printer in results)
        {
            using (printer) printers.Add(CreatePrinterInfo(printer));
        }
        return printers.OrderBy(printer => printer.Name, StringComparer.OrdinalIgnoreCase).ToArray();
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

        return WindowsPrinterClassifier.Classify(new PrinterInfo
        {
            Name = GetString(printer, "Name") ?? string.Empty,
            IsDefault = GetBoolean(printer, "Default") ?? false,
            DriverName = GetString(printer, "DriverName"),
            PortName = GetString(printer, "PortName"),
            Status = status,
            IsOnline = WindowsPrinterStatusMapper.IsOnline(status),
            WorkOffline = workOffline,
            Capabilities = ["raw", "escpos"]
        }, GetUInt16(printer, "MarkingTechnology"));
    }

    private static string? GetString(ManagementBaseObject printer, string propertyName) =>
        GetProperty(printer, propertyName) as string;

    private static bool? GetBoolean(ManagementBaseObject printer, string propertyName) =>
        GetProperty(printer, propertyName) as bool?;

    private static ushort? GetUInt16(ManagementBaseObject printer, string propertyName) =>
        GetProperty(printer, propertyName) as ushort?;

    private static object? GetProperty(ManagementBaseObject printer, string propertyName)
    {
        try { return printer[propertyName]; }
        catch (ManagementException) { return null; }
        catch (COMException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    private static void EnsureWindows()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException("The Windows printer provider requires Windows.");
        }
    }
}
