using System.Management;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using OpenThermalPrintAgent.Core.Models;

namespace OpenThermalPrintAgent.Windows.Tests;

[SupportedOSPlatform("windows")]
public sealed class WindowsPrinterDiscoveryTests
{
    [Theory]
    [InlineData("EPSON TM-T88VI", null, null, "likelyThermal")]
    [InlineData("Receipt counter", "EPSON TM-m30II", null, "likelyThermal")]
    [InlineData("Star TSP143III", null, null, "likelyThermal")]
    [InlineData("Citizen CT-S310II", null, null, "likelyThermal")]
    [InlineData("XP-80", null, null, "likelyThermal")]
    [InlineData("XP-80", "XP-80", null, "likelyThermal")]
    [InlineData("XP-80C", null, null, "likelyThermal")]
    [InlineData("Xprinter XP-80C", null, null, "likelyThermal")]
    [InlineData("Counter", "Xprinter XP-80", null, "likelyThermal")]
    [InlineData("xp-80c (Copy 1)", null, null, "likelyThermal")]
    [InlineData("Xprinter", null, null, "unknown")]
    [InlineData("XP-800", null, null, "unknown")]
    [InlineData("XP-80CS", null, null, "unknown")]
    [InlineData("PrefixXP-80C", null, null, "unknown")]
    [InlineData("XP-80C_suffix", null, null, "unknown")]
    [InlineData("POS80", null, null, "unknown")]
    [InlineData("EPSON", null, null, "unknown")]
    [InlineData("EPSON TM-U220", null, null, "unknown")]
    [InlineData("EPSON EcoTank", null, null, "unknown")]
    [InlineData("EPSON TM-T880", null, null, "unknown")]
    [InlineData("NotEPSON TM-T88VI", null, null, "unknown")]
    [InlineData("Zebra ZD421", null, null, "unknown")]
    [InlineData("HP LaserJet", "HP LaserJet", null, "unknown")]
    [InlineData("POS-80", "Generic / Text Only", null, "unknown")]
    [InlineData("Counter", null, (ushort)16, "likelyThermal")]
    [InlineData("Counter", null, (ushort)17, "likelyThermal")]
    [InlineData("Counter", null, (ushort)18, "likelyThermal")]
    [InlineData("Counter", null, (ushort)19, "likelyThermal")]
    [InlineData("EPSON TM-T88VI", null, (ushort)4, "notThermal")]
    [InlineData("Counter", null, (ushort)12, "notThermal")]
    [InlineData("Counter", null, (ushort)2, "unknown")]
    [InlineData("Counter", null, (ushort)0, "unknown")]
    [InlineData("Counter", null, (ushort)99, "unknown")]
    public void ClassificationIsConservative(string name, string? driver, ushort? technology, string expected)
    {
        var result = WindowsPrinterClassifier.Classify(new PrinterInfo { Name = name, DriverName = driver }, technology);

        Assert.Equal(expected, result.ThermalClassification);
        Assert.Null(result.IsVirtual);
        Assert.Empty(result.Capabilities);
    }

    [Theory]
    [InlineData("Microsoft Print to PDF", null, null)]
    [InlineData("Microsoft XPS Document Writer", null, null)]
    [InlineData("Fax", null, null)]
    [InlineData("PDFCreator", null, null)]
    [InlineData("AdobePDF", "AdobePDF", null)]
    [InlineData("OneNote", "OneNote", null)]
    [InlineData("Counter", "PDFCreator", "PORTPROMPT:")]
    [InlineData("Counter", "Microsoft Print To PDF", null)]
    public void VirtualQueuesOverrideThermalEvidence(string name, string? driver, string? port)
    {
        var result = WindowsPrinterClassifier.Classify(new PrinterInfo { Name = name, DriverName = driver, PortName = port }, 17);

        Assert.True(result.IsVirtual);
        Assert.Equal("notThermal", result.ThermalClassification);
    }

    [Fact]
    public void AdobePdfWithMissingTechnologyIsVirtual()
    {
        var result = WindowsPrinterClassifier.Classify(new PrinterInfo { Name = "AdobePDF", DriverName = "AdobePDF" });
        Assert.True(result.IsVirtual);
        Assert.Equal("notThermal", result.ThermalClassification);
    }

    [Fact]
    public void LegacyJsonDoesNotRequireNewMetadata()
    {
        var printer = JsonSerializer.Deserialize<PrinterInfo>("""{"name":"Counter"}""", new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(printer);
        Assert.Null(printer.IsVirtual);
        Assert.Null(printer.ThermalClassification);
    }

    [Fact]
    public void WireNamesMatchContract()
    {
        var printer = WindowsPrinterClassifier.Classify(new PrinterInfo { Name = "Counter", DriverName = "EPSON TM-T20III" });
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(printer, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal("likelyThermal", json.RootElement.GetProperty("thermalClassification").GetString());
        Assert.Equal("EPSON TM-T20III", json.RootElement.GetProperty("driverName").GetString());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("isVirtual").ValueKind);
    }

    [Theory]
    [InlineData("wmi")]
    [InlineData("com")]
    [InlineData("access")]
    public void WmiFailurePreservesFallbackQueues(string failure)
    {
        Exception error = failure switch
        {
            "wmi" => new ManagementException(),
            "com" => new COMException(),
            _ => new UnauthorizedAccessException()
        };
        var fallback = new[] { WindowsPrinterClassifier.Classify(new PrinterInfo { Name = "Counter" }) };
        var result = WindowsPrinterProvider.Discover(() => throw error, () => fallback);
        Assert.Same(fallback, result);
        Assert.Equal("unknown", result[0].ThermalClassification);
    }

    [Fact]
    public void SuccessfulWmiDoesNotInvokeFallback()
    {
        var printers = new[] { new PrinterInfo { Name = "Counter" } };
        Assert.Same(printers, WindowsPrinterProvider.Discover(() => printers, () => throw new InvalidOperationException()));
    }

    [Fact]
    public void SuccessfulEmptySpoolerProbeIgnoresStaleError()
    {
        WindowsSpoolerDiscovery.ValidateProbeResult(true, 0, 5);
    }

    [Fact]
    public void InsufficientBufferProbeAllowsEnumeration()
    {
        WindowsSpoolerDiscovery.ValidateProbeResult(false, 256, 122);
    }

    [Theory]
    [InlineData(0u, 5)]
    [InlineData(256u, 5)]
    [InlineData(0u, 122)]
    public void FailedSpoolerProbePreservesError(uint needed, int error)
    {
        var exception = Assert.Throws<Win32Exception>(() => WindowsSpoolerDiscovery.ValidateProbeResult(false, needed, error));
        Assert.Equal(error, exception.NativeErrorCode);
    }
}
