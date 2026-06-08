using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace OpenThermalPrintAgent.Host.Tests;

public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/api/v1/health")]
    [InlineData("/health")]
    public async Task HealthReportsAgentAndApiVersion(string path)
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(path);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("ok", body.Status);
        Assert.Equal("open-thermal-print-agent", body.Name);
        Assert.False(string.IsNullOrWhiteSpace(body.Version));
        Assert.Equal(body.Version, body.AgentVersion);
        Assert.Equal("v1", body.ApiVersion);
        Assert.False(string.IsNullOrWhiteSpace(body.Platform));
    }

    private sealed record HealthResponse(
        string Status,
        string Name,
        string Version,
        string AgentVersion,
        string ApiVersion,
        string Platform);
}
