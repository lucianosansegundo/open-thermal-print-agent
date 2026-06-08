using OpenThermalPrintAgent.Windows;

namespace OpenThermalPrintAgent.Windows.Tests;

public sealed class WindowsPrinterStatusMapperTests
{
    [Theory]
    [InlineData((ushort)3, null, false, "idle")]
    [InlineData((ushort)4, null, false, "printing")]
    [InlineData((ushort)5, null, false, "warmingUp")]
    [InlineData((ushort)6, null, false, "stopped")]
    [InlineData((ushort)7, null, false, "offline")]
    [InlineData(null, null, true, "offline")]
    [InlineData(null, (ushort)4, false, "noPaper")]
    [InlineData(null, (ushort)8, false, "jammed")]
    public void MapStatusNormalizesWindowsValues(ushort? printerStatus, ushort? detectedErrorState, bool workOffline, string expected)
    {
        var status = WindowsPrinterStatusMapper.MapStatus(printerStatus, detectedErrorState, workOffline);

        Assert.Equal(expected, status);
    }

    [Theory]
    [InlineData("idle", true)]
    [InlineData("printing", true)]
    [InlineData("offline", false)]
    [InlineData("noPaper", false)]
    [InlineData("unknown", null)]
    public void IsOnlineMapsKnownStatuses(string status, bool? expected)
    {
        var isOnline = WindowsPrinterStatusMapper.IsOnline(status);

        Assert.Equal(expected, isOnline);
    }
}
