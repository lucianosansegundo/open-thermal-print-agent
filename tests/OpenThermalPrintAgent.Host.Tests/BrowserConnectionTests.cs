using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenThermalPrintAgent.Host.Configuration;

namespace OpenThermalPrintAgent.Host.Tests;

public sealed class BrowserConnectionTests : IDisposable
{
    private const string Origin = "https://pos.example";
    private const string Secret = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    private readonly string directory = Path.Combine(Path.GetTempPath(), "thermal-browser-" + Guid.NewGuid().ToString("N"));
    private readonly DateTimeOffset now = DateTimeOffset.UtcNow;
    private LocalConnection Connection(string owner = "owner") => new(directory, owner, new AgentOptions
    {
        AllowedOrigins = [Origin], Security = new() { RequireToken = true, Token = "private-token" }
    });
    private static string UriFor(string requestId) => "open-thermal-print-agent://connect/" + requestId;

    [Fact]
    public void RequiresLocalApprovalAndSecretThenConsumesOnce()
    {
        var connection = Connection();
        var pending = connection.StartBrowser(Origin, Secret, now)!;
        Assert.Equal((202, (string?)null), connection.RedeemBrowser(Origin, pending.RequestId, Secret, now));
        Assert.Equal(403, connection.RedeemBrowser(Origin, pending.RequestId, new string('B', 64), now).Status);
        Assert.False(Connection("another-owner").ApproveBrowser(UriFor(pending.RequestId), now));
        Assert.False(connection.ApproveBrowser(UriFor(new string('0', 32)), now));
        Assert.True(connection.ApproveBrowser(UriFor(pending.RequestId), now));
        Assert.Equal((200, "private-token"), connection.RedeemBrowser(Origin, pending.RequestId, Secret, now));
        Assert.Equal(403, connection.RedeemBrowser(Origin, pending.RequestId, Secret, now).Status);
    }

    [Fact]
    public async Task ConcurrentRedemptionReleasesOnlyOneToken()
    {
        var pending = Connection().StartBrowser(Origin, Secret, now)!;
        Connection().ApproveBrowser(UriFor(pending.RequestId), now);
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() => Connection().RedeemBrowser(Origin, pending.RequestId, Secret, now))));
        Assert.Single(results.Where(result => result.Status == 200));
    }

    [Fact]
    public void ExpiredAndReplacedRequestsCannotBeApprovedOrRedeemed()
    {
        var connection = Connection();
        var old = connection.StartBrowser(Origin, Secret, now)!;
        var current = connection.StartBrowser(Origin, Secret, now)!;
        Assert.False(connection.ApproveBrowser(UriFor(old.RequestId), now));
        Assert.False(connection.ApproveBrowser(UriFor(current.RequestId), now.AddMinutes(5)));
        Assert.Equal(410, connection.RedeemBrowser(Origin, current.RequestId, Secret, now.AddMinutes(5)).Status);
        Assert.Equal(403, connection.RedeemBrowser("", current.RequestId, Secret, now).Status);
        Assert.Equal(403, connection.RedeemBrowser("https://attacker.example", current.RequestId, Secret, now).Status);
        var state = File.ReadAllText(Path.Combine(directory, "browser-connection.json"));
        Assert.DoesNotContain(Secret, state);
        Assert.DoesNotContain("private-token", state);
    }

    [Theory]
    [InlineData("open-thermal-print-agent://connect/../AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("open-thermal-print-agent://connect/%2fAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("open-thermal-print-agent://connect/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA?x=1")]
    [InlineData("open-thermal-print-agent://connect/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA#x")]
    [InlineData("OPEN-THERMAL-PRINT-AGENT://connect/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("open-thermal-print-agent://connect:1/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("open-thermal-print-agent://user@connect/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("open-thermal-print-agent:\\connect\\AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("open-thermal-print-agent://connect/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\" --stop")]
    public void RejectsNonCanonicalProtocolArguments(string uri) => Assert.Null(LocalConnection.RequestIdFromUri(uri));

    [Fact]
    public void CliRejectsExtraArgumentsBeforeReadingConfiguration()
    {
        Assert.Equal(1, InstalledRuntime.Command(["--approve-connection", UriFor(new string('A', 32)), "--stop"]));
        Assert.Equal(1, InstalledRuntime.Command(["--approve-connection"]));
    }

    [Fact]
    public async Task HttpEndpointsRejectMissingOriginAndSecretBeforeReturningPending()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<LocalConnection>();
            services.AddSingleton(Connection());
        }));
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/v1/pairing/browser/start", new { secret = Secret })).StatusCode);
        client.DefaultRequestHeaders.Add("Origin", "https://attacker.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/v1/pairing/browser/start", new { secret = Secret })).StatusCode);
        client.DefaultRequestHeaders.Remove("Origin");
        client.DefaultRequestHeaders.Add("Origin", Origin);
        var start = await client.PostAsJsonAsync("/api/v1/pairing/browser/start", new { secret = Secret });
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        var body = await start.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonObject>();
        var requestId = body!["requestId"]!.GetValue<string>();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/v1/pairing/browser/complete", new { requestId, secret = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/v1/pairing/browser/complete", new { requestId, secret = Secret })).StatusCode);
        Connection().ApproveBrowser(UriFor(requestId), DateTimeOffset.UtcNow);
        var completed = await client.PostAsJsonAsync("/api/v1/pairing/browser/complete", new { requestId, secret = Secret });
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/v1/pairing/browser/complete", new { requestId, secret = Secret })).StatusCode);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}
