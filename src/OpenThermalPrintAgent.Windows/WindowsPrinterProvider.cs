using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;
using OpenThermalPrintAgent.Core.Models;
using OpenThermalPrintAgent.Core.Printing;

namespace OpenThermalPrintAgent.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsPrinterProvider : IPrinterProvider
{
    private const string PrintersRegistryPath = @"SYSTEM\CurrentControlSet\Control\Print\Printers";
    private const string UserWindowsRegistryPath = @"Software\Microsoft\Windows NT\CurrentVersion\Windows";

    public IReadOnlyList<PrinterInfo> ListPrinters()
    {
        EnsureWindows();

        using var printersKey = Registry.LocalMachine.OpenSubKey(PrintersRegistryPath);
        var defaultPrinterName = GetDefaultPrinterName();

        return printersKey?.GetSubKeyNames()
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(name => new PrinterInfo
            {
                Name = name,
                IsDefault = string.Equals(name, defaultPrinterName, StringComparison.OrdinalIgnoreCase),
                Capabilities = ["raw", "escpos"]
            })
            .ToArray() ?? [];
    }

    public bool PrinterExists(string printerName)
    {
        if (string.IsNullOrWhiteSpace(printerName))
        {
            return false;
        }

        return ListPrinters().Any(printer => string.Equals(printer.Name, printerName, StringComparison.OrdinalIgnoreCase));
    }

    private static string? GetDefaultPrinterName()
    {
        using var userWindowsKey = Registry.CurrentUser.OpenSubKey(UserWindowsRegistryPath);
        var deviceValue = userWindowsKey?.GetValue("Device") as string;

        return string.IsNullOrWhiteSpace(deviceValue)
            ? null
            : deviceValue.Split(',', 2)[0];
    }

    private static void EnsureWindows()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException("The Windows printer provider requires Windows.");
        }
    }
}
