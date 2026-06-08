using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenThermalPrintAgent.Core.Models;
using OpenThermalPrintAgent.Core.Printing;

namespace OpenThermalPrintAgent.Host.Tests;

public sealed class WebSocketEndpointTests
{
    [Fact]
    public async Task WebSocketHealthReturnsAgentStatus()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var webSocket = await factory.Server.CreateWebSocketClient().ConnectAsync(new Uri("ws://localhost/api/v1/ws"), CancellationToken.None);

        await SendAsync(webSocket, """{"type":"health"}""");
        var response = await ReceiveAsync(webSocket);

        using var document = JsonDocument.Parse(response);
        Assert.Equal("health", document.RootElement.GetProperty("type").GetString());
        Assert.Equal("ok", document.RootElement.GetProperty("status").GetString());
        Assert.Equal("v1", document.RootElement.GetProperty("apiVersion").GetString());
    }

    [Fact]
    public async Task WebSocketPrintReturnsPrintResult()
    {
        using var factory = CreateFactory();
        using var webSocket = await factory.Server.CreateWebSocketClient().ConnectAsync(new Uri("ws://localhost/api/v1/ws"), CancellationToken.None);

        await SendAsync(webSocket, """
        {
          "type": "print",
          "payload": {
            "jobId": "ws-test",
            "printerName": "TestPrinter",
            "format": "escpos",
            "paperWidth": "80mm",
            "options": { "cut": false, "copies": 1 },
            "content": [
              { "type": "text", "value": "WebSocket test" }
            ]
          }
        }
        """);
        var response = await ReceiveAsync(webSocket);

        using var document = JsonDocument.Parse(response);
        Assert.Equal("printResult", document.RootElement.GetProperty("type").GetString());
        Assert.Equal("ws-test", document.RootElement.GetProperty("jobId").GetString());
        Assert.Equal("printed", document.RootElement.GetProperty("status").GetString());
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPrinterProvider>();
                services.RemoveAll<IRawPrinter>();
                services.AddSingleton<IPrinterProvider, FakePrinterProvider>();
                services.AddSingleton<IRawPrinter, NoOpRawPrinter>();
            });
        });
    }

    private static async Task SendAsync(WebSocket webSocket, string message)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        await webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private static async Task<string> ReceiveAsync(WebSocket webSocket)
    {
        var buffer = new byte[4096];
        var result = await webSocket.ReceiveAsync(buffer, CancellationToken.None);
        return Encoding.UTF8.GetString(buffer, 0, result.Count);
    }

    private sealed class FakePrinterProvider : IPrinterProvider
    {
        public IReadOnlyList<PrinterInfo> ListPrinters() =>
        [
            new PrinterInfo
            {
                Name = "TestPrinter",
                IsDefault = true,
                Status = "idle",
                IsOnline = true,
                Capabilities = ["raw", "escpos"]
            }
        ];

        public bool PrinterExists(string printerName) =>
            string.Equals(printerName, "TestPrinter", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class NoOpRawPrinter : IRawPrinter
    {
        public void PrintRaw(string printerName, ReadOnlySpan<byte> bytes)
        {
        }
    }
}
