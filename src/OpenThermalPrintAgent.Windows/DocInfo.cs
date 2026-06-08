using System.Runtime.InteropServices;

namespace OpenThermalPrintAgent.Windows;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct DocInfo
{
    [MarshalAs(UnmanagedType.LPWStr)]
    public string DocumentName;

    [MarshalAs(UnmanagedType.LPWStr)]
    public string? OutputFile;

    [MarshalAs(UnmanagedType.LPWStr)]
    public string DataType;
}
