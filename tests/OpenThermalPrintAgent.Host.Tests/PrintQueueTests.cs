using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenThermalPrintAgent.Core.Models;
using OpenThermalPrintAgent.Core.Printing;

namespace OpenThermalPrintAgent.Host.Tests;

public sealed class PrintQueueTests
{
    [Fact]
    public async Task PrintEndpointQueuesJobWhenQueueIsEnabled()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/print", new
        {
            jobId = "queued-test",
            printerName = "TestPrinter",
            format = "escpos",
            paperWidth = "80mm",
            options = new { cut = false, copies = 1 },
            content = new[]
            {
                new { type = "text", value = "Queued test" }
            }
        });
        var body = await response.Content.ReadFromJsonAsync<PrintResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("queued-test", body.JobId);
        Assert.Equal("queued", body.Status);

        var recentJobs = await WaitForPrintedJobAsync(client);
        Assert.Contains(recentJobs, job => job.JobId == "queued-test" && job.Status == "printed");
    }

    private static async Task<QueuedJob[]> WaitForPrintedJobAsync(HttpClient client)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var jobs = await client.GetFromJsonAsync<QueuedJob[]>("/api/v1/jobs/recent") ?? [];
            if (jobs.Any(job => job.JobId == "queued-test" && job.Status == "printed"))
            {
                return jobs;
            }

            await Task.Delay(50);
        }

        return await client.GetFromJsonAsync<QueuedJob[]>("/api/v1/jobs/recent") ?? [];
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Agent:Queue:Enabled"] = "true",
                    ["Agent:Queue:RetryAttempts"] = "1",
                    ["Agent:Queue:RetryDelayMilliseconds"] = "1"
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

    private sealed record PrintResponse(string JobId, string Status, string PrinterName, DateTimeOffset PrintedAt);

    private sealed record QueuedJob(string JobId, string PrinterName, string Status, int Attempts);

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
