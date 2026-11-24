using OpenThermalPrintAgent.Host.Configuration;
using Microsoft.Extensions.Configuration;

namespace OpenThermalPrintAgent.Host.Tests;

public sealed class LocalConnectionTests : IDisposable
{
    private const string Origin = "https://pos.example";
    private readonly string directory = Path.Combine(Path.GetTempPath(), "thermal-connection-" + Guid.NewGuid().ToString("N"));
    private readonly DateTimeOffset now = DateTimeOffset.UtcNow;
    private static AgentOptions Options => new()
    {
        AllowedOrigins = [Origin],
        Security = new() { RequireToken = true, Token = "private-device-token" }
    };

    private LocalConnection Connection(string owner = "installation-one") => new(directory, owner, Options);
    private string Nonce(LocalConnection connection) => connection.Create(Origin, now).Split('=')[1];

    [Fact]
    public void HandoffHasOnlyNonceInFragmentAndStoresOnlyHash()
    {
        var connection = Connection();
        var url = connection.Create(Origin, now);
        var uri = new Uri(url);
        Assert.Equal("", uri.Query);
        Assert.Equal(64, uri.Fragment.Split('=')[1].Length);
        var file = File.ReadAllText(Path.Combine(directory, "connection.json"));
        Assert.DoesNotContain(uri.Fragment.Split('=')[1], file);
        Assert.DoesNotContain(Options.Security.Token!, file);
        Assert.DoesNotContain(Options.Security.Token!, url);
    }

    [Fact]
    public void RedeemsOnceAndRejectsReplay()
    {
        var connection = Connection();
        var nonce = Nonce(connection);
        Assert.Equal(Options.Security.Token, connection.Redeem(Origin, nonce, now));
        Assert.Null(connection.Redeem(Origin, nonce, now));
    }

    [Fact]
    public async Task ConcurrentInstancesReleaseOnlyOneToken()
    {
        var nonce = Nonce(Connection());
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() => Connection().Redeem(Origin, nonce, now))));
        Assert.Single(results.Where(result => result is not null));
    }

    [Fact]
    public async Task CreatingHandoffsAndRedeemingConcurrentlyPreservesLatestGrant()
    {
        var tasks = Enumerable.Range(0, 100).Select(_ => Task.Run(() =>
        {
            var connection = Connection();
            var nonce = Nonce(connection);
            connection.Redeem(Origin, nonce, now);
        }));
        await Task.WhenAll(tasks);
        var latest = Nonce(Connection());
        Assert.Equal(Options.Security.Token, Connection().Redeem(Origin, latest, now));
    }

    [Fact]
    public void RejectsWrongOriginNonceOwnerAndExpiryWithoutReleasingToken()
    {
        var connection = Connection();
        var nonce = Nonce(connection);
        Assert.Null(connection.Redeem("https://attacker.example", nonce, now));
        Assert.Null(connection.Redeem("", nonce, now));
        Assert.Null(connection.Redeem(Origin, new string('A', 64), now));
        Assert.Null(Connection("another-installation").Redeem(Origin, nonce, now));
        Assert.Null(connection.Redeem(Origin, nonce, now.AddMinutes(5)));
        Assert.Equal(Options.Security.Token, connection.Redeem(Origin, nonce, now));
    }

    [Fact]
    public void NewHandoffRevokesPreviousAndMalformedFilesFailClosed()
    {
        var connection = Connection();
        var oldNonce = Nonce(connection);
        var newNonce = Nonce(connection);
        Assert.Null(connection.Redeem(Origin, oldNonce, now));
        Assert.Equal(Options.Security.Token, connection.Redeem(Origin, newNonce, now));
        File.WriteAllText(Path.Combine(directory, "connection.json"), "{bad-json");
        Assert.Null(connection.Redeem(Origin, newNonce, now));
    }

    [Fact]
    public void RejectsUnconfiguredAndInsecureOrigins()
    {
        Assert.Throws<InvalidOperationException>(() => Connection().Create("https://attacker.example", now));
        Assert.Throws<InvalidOperationException>(() => Connection().Create("http://pos.example", now));
    }

    [Fact]
    public void HttpUpgradePreservesConfigurationAndPreparesPrivateManualFallbackWithoutGrant()
    {
        var configPath = Path.Combine(directory, "agent.json");
        InstalledConfiguration.Ensure(configPath, "http://legacy-pos.example");
        var original = File.ReadAllText(configPath);
        InstalledConfiguration.Ensure(configPath, Origin);
        var configuration = new ConfigurationBuilder().AddJsonFile(configPath).Build();
        var options = configuration.GetSection("Agent").Get<AgentOptions>()!;
        var connection = new LocalConnection(directory, configuration["InstallationId"], options);

        var url = connection.Prepare(now);

        Assert.Equal(original, File.ReadAllText(configPath));
        Assert.Equal("file", new Uri(url).Scheme);
        Assert.Equal("", new Uri(url).Fragment);
        Assert.False(File.Exists(Path.Combine(directory, "connection.json")));
        var html = File.ReadAllText(Path.Combine(directory, "setup.html"));
        Assert.Contains("Automatic browser connection requires a configured HTTPS site origin", html);
        Assert.Contains("Advanced manual connection", html);
        Assert.Contains(options.Security.Token!, html);
        Assert.DoesNotContain(options.Security.Token!, url);
        Assert.Null(connection.Redeem("http://legacy-pos.example", new string('A', 64), now));
    }

    [Fact]
    public void PrefersFirstConfiguredHttpsOriginAndKeepsPermanentTokenOutOfHelper()
    {
        var options = new AgentOptions
        {
            AllowedOrigins = ["http://legacy-pos.example", Origin, "https://second.example"],
            Security = Options.Security
        };
        var connection = new LocalConnection(directory, "installation-one", options);

        var url = connection.Prepare(now);

        Assert.StartsWith(Origin + "/#rw-print-agent-connect=", url);
        var html = File.ReadAllText(Path.Combine(directory, "setup.html"));
        Assert.Contains(url, html);
        Assert.DoesNotContain(options.Security.Token!, html);
        Assert.Equal(options.Security.Token, connection.Redeem(Origin, url.Split('=')[1], now));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}
