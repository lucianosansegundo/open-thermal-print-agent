namespace OpenThermalPrintAgent.Windows;

public static class WindowsPrinterStatusMapper
{
    public static string MapStatus(ushort? printerStatus, ushort? detectedErrorState, bool? workOffline)
    {
        if (workOffline == true)
        {
            return "offline";
        }

        if (detectedErrorState is not null and not 0 and not 2)
        {
            return detectedErrorState.Value switch
            {
                3 => "lowPaper",
                4 => "noPaper",
                5 => "lowToner",
                6 => "noToner",
                7 => "doorOpen",
                8 => "jammed",
                9 => "offline",
                10 => "serviceRequested",
                11 => "outputBinFull",
                _ => "error"
            };
        }

        return printerStatus switch
        {
            3 => "idle",
            4 => "printing",
            5 => "warmingUp",
            6 => "stopped",
            7 => "offline",
            1 => "other",
            2 => "unknown",
            _ => "unknown"
        };
    }

    public static bool? IsOnline(string status)
    {
        return status switch
        {
            "idle" or "printing" or "warmingUp" => true,
            "offline" or "stopped" or "error" or "noPaper" or "doorOpen" or "jammed" => false,
            _ => null
        };
    }
}
