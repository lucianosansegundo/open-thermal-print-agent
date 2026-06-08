using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenThermalPrintAgent.Core.Models;
using OpenThermalPrintAgent.Core.Printing;

namespace OpenThermalPrintAgent.Host.Tests;

public sealed class PrintTokenSecurityTests
{
    [Fact]
    public async Task PrintEndpointReturnsUnauthorizedWhenTokenIsMissing()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/print/test", ValidRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PrintEndpointReturnsForbiddenWhenTokenIsInvalid()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-OpenThermalPrintAgent-Token", "wrong-token");

        var response = await client.PostAsJsonAsync("/api/v1/print/test", ValidRequest());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PrintEndpointAcceptsConfiguredHeaderToken()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-OpenThermalPrintAgent-Token", "test-token");

        var response = await client.PostAsJsonAsync("/api/v1/print/test", ValidRequest());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PrintEndpointAcceptsBearerToken()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "test-token");

        var response = await client.PostAsJsonAsync("/api/v1/print/test", ValidRequest());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Agent:Security:RequireToken"] = "true",
                    ["Agent:Security:Token"] = "test-token"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPrinterProvider>();
                services.RemoveAll<IRawPrinter>();
                services.AddSingleton<IPrinterProvider, FakePrinterProvider>();
                services.AddSingleton<IRawPrinter, NoOpRawPrinter>();
            });
        });
    }

    private static object ValidRequest() => new
    {
        printerName = FakePrinterProvider.PrinterName,
        paperWidth = "80mm",
        cut = false,
        openDrawer = false
    };

    private sealed class FakePrinterProvider : IPrinterProvider
    {
        public const string PrinterName = "TestPrinter";

        public IReadOnlyList<PrinterInfo> ListPrinters() =>
        [
            new PrinterInfo
            {
                Name = PrinterName,
                IsDefault = true,
                Status = "idle",
                IsOnline = true,
                Capabilities = ["raw", "escpos"]
            }
        ];

        public bool PrinterExists(string printerName) =>
            string.Equals(printerName, PrinterName, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class NoOpRawPrinter : IRawPrinter
    {
        public void PrintRaw(string printerName, ReadOnlySpan<byte> bytes)
        {
        }
    }
}
