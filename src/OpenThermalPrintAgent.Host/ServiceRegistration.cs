using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using OpenThermalPrintAgent.Core.Printing;
using OpenThermalPrintAgent.Windows;

namespace OpenThermalPrintAgent.Host;

internal static class ServiceRegistration
{
    public static void AddPlatformPrinting(this IServiceCollection services)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            AddWindowsPrinting(services);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void AddWindowsPrinting(IServiceCollection services)
    {
        services.AddSingleton<IPrinterProvider, WindowsPrinterProvider>();
        services.AddSingleton<IRawPrinter, WindowsRawPrinter>();
    }
}
