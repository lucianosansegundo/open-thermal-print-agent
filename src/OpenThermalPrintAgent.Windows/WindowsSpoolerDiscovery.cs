using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using OpenThermalPrintAgent.Core.Models;

namespace OpenThermalPrintAgent.Windows;

[SupportedOSPlatform("windows")]
internal static class WindowsSpoolerDiscovery
{
    [StructLayout(LayoutKind.Sequential)]
    private struct PrinterInfo4
    {
        public IntPtr Name;
        public IntPtr ServerName;
        public uint Attributes;
    }

    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool EnumPrinters(uint flags, string? name, uint level,
        IntPtr buffer, uint size, out uint needed, out uint returned);

    internal static IReadOnlyList<PrinterInfo> ListPrinters()
    {
        // Level 4 enumerates local and connected queues without querying their drivers.
        var success = EnumPrinters(6, null, 4, IntPtr.Zero, 0, out var needed, out _);
        ValidateProbeResult(success, needed, success ? 0 : Marshal.GetLastWin32Error());
        if (needed == 0)
        {
            return [];
        }
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var size = needed;
            var buffer = Marshal.AllocHGlobal(checked((int)size));
            try
            {
                if (!EnumPrinters(6, null, 4, buffer, size, out needed, out var count))
                {
                    var error = Marshal.GetLastWin32Error();
                    if (error == 122) continue;
                    throw new Win32Exception(error);
                }
                var printers = new List<PrinterInfo>();
                for (var index = 0; index < count; index++)
                {
                    var item = Marshal.PtrToStructure<PrinterInfo4>(IntPtr.Add(buffer,
                        checked(index * Marshal.SizeOf<PrinterInfo4>())));
                    var name = Marshal.PtrToStringUni(item.Name);
                    if (!string.IsNullOrWhiteSpace(name))
                        printers.Add(WindowsPrinterClassifier.Classify(new PrinterInfo { Name = name, Status = "unknown" }));
                }
                return printers.OrderBy(printer => printer.Name, StringComparer.OrdinalIgnoreCase).ToArray();
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        throw new Win32Exception(122);
    }

    internal static void ValidateProbeResult(bool success, uint needed, int error)
    {
        // Last-error is undefined on success; a failed size probe must request a larger buffer.
        if (!success && (needed == 0 || error != 122))
            throw new Win32Exception(error);
    }
}
