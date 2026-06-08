using System.Runtime.InteropServices;
using OpenThermalPrintAgent.Core.Printing;

namespace OpenThermalPrintAgent.Windows;

public sealed class WindowsRawPrinter : IRawPrinter
{
    public void PrintRaw(string printerName, ReadOnlySpan<byte> bytes)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException("Raw Windows printing requires Windows.");
        }

        if (string.IsNullOrWhiteSpace(printerName))
        {
            throw new ArgumentException("Printer name is required.", nameof(printerName));
        }

        if (bytes.IsEmpty)
        {
            throw new ArgumentException("Print data is required.", nameof(bytes));
        }

        var documentInfo = new DocInfo
        {
            DocumentName = "Open Thermal Print Agent Job",
            DataType = "RAW"
        };

        if (!NativeMethods.OpenPrinter(printerName, out var printerHandle, IntPtr.Zero))
        {
            throw CreateWin32Exception("OpenPrinter failed");
        }

        try
        {
            if (NativeMethods.StartDocPrinter(printerHandle, 1, ref documentInfo) == 0)
            {
                throw CreateWin32Exception("StartDocPrinter failed");
            }

            try
            {
                if (!NativeMethods.StartPagePrinter(printerHandle))
                {
                    throw CreateWin32Exception("StartPagePrinter failed");
                }

                try
                {
                    var buffer = bytes.ToArray();
                    if (!NativeMethods.WritePrinter(printerHandle, buffer, buffer.Length, out var written) || written != buffer.Length)
                    {
                        throw CreateWin32Exception("WritePrinter failed");
                    }
                }
                finally
                {
                    NativeMethods.EndPagePrinter(printerHandle);
                }
            }
            finally
            {
                NativeMethods.EndDocPrinter(printerHandle);
            }
        }
        finally
        {
            NativeMethods.ClosePrinter(printerHandle);
        }
    }

    private static InvalidOperationException CreateWin32Exception(string message)
    {
        var error = Marshal.GetLastWin32Error();
        return new InvalidOperationException($"{message}. Win32 error: {error}.");
    }
}
