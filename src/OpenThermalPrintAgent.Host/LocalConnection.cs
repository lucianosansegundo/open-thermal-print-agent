using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Net;
using OpenThermalPrintAgent.Host.Configuration;

namespace OpenThermalPrintAgent.Host;

public sealed class LocalConnection(string directory, string? installationId, AgentOptions options)
{
    private string HandoffPath => Path.Combine(directory, "connection.json");
    private string MutexName => (OperatingSystem.IsWindows() ? @"Global\" : "") +
        "OpenThermalPrintAgent-connection-" + Hash(Path.GetFullPath(HandoffPath).ToUpperInvariant());

    public string Prepare(DateTimeOffset now)
    {
        var origin = options.AllowedOrigins.FirstOrDefault(value => value.StartsWith("https://", StringComparison.Ordinal));
        var setupPath = Path.Combine(directory, "setup.html");
        var url = origin is null ? new Uri(Path.GetFullPath(setupPath)).AbsoluteUri : Create(origin, now);
        var instructions = origin is null
            ? $$"""
                <p>Automatic browser connection requires a configured HTTPS site origin. Your existing HTTP site and local token are preserved.</p>
                <p>Use the POS advanced connection settings for this HTTP installation. Keep this local file private to your Windows account.</p>
                <details><summary>Advanced manual connection</summary><label for="token">Local agent token</label>
                <input id="token" type="password" readonly value="{{WebUtility.HtmlEncode(options.Security.Token)}}" onclick="this.select()"></details>
                """
            : $$"""
                <p><a href="{{WebUtility.HtmlEncode(url)}}">Connect your POS browser</a></p>
                <p>This private link expires in five minutes and works once. Open it in the browser profile used for your POS.</p>
                <p>Allow local network access when your browser asks. Automatic browser connection requires HTTPS.</p>
                """;
        Directory.CreateDirectory(directory);
        File.WriteAllText(setupPath, $$"""
            <!doctype html><html lang="en"><meta charset="utf-8"><title>Connect Open Thermal Print Agent</title>
            <style>body{font:18px system-ui;max-width:850px;margin:3em auto;padding:1em}input{width:100%;padding:10px}</style>
            <h1>Connect your printer agent</h1>{{instructions}}
            <p>To reconnect, use Connect Open Thermal Print Agent from the Windows Start menu.</p>
            <p>Agent URL <code>http://127.0.0.1:17890/api/v1</code></p></html>
            """);
        return url;
    }

    public string Create(string origin, DateTimeOffset now)
    {
        InstalledConfiguration.ValidateOrigin(origin);
        if (!origin.StartsWith("https://", StringComparison.Ordinal) ||
            !options.AllowedOrigins.Contains(origin, StringComparer.Ordinal) || string.IsNullOrWhiteSpace(installationId))
            throw new InvalidOperationException("Automatic connection requires a configured exact HTTPS site origin.");

        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var handoff = new Handoff(installationId, Hash(nonce), origin, now.AddMinutes(5));
        using var mutex = AcquireHandoffLock();
        var temporaryPath = HandoffPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(handoff));
            File.Move(temporaryPath, HandoffPath, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
            mutex.ReleaseMutex();
        }
        return origin + "/#rw-print-agent-connect=" + nonce;
    }

    public IResult Complete(HttpRequest request, ConnectionRequest body)
    {
        var token = Redeem(request.Headers.Origin.ToString(), body.Nonce, DateTimeOffset.UtcNow);
        return token is null ? Results.StatusCode(403) : Results.Ok(new { token });
    }

    internal string? Redeem(string origin, string? nonce, DateTimeOffset now)
    {
        if (!options.Security.RequireToken || string.IsNullOrWhiteSpace(options.Security.Token) ||
            !options.AllowedOrigins.Contains(origin, StringComparer.Ordinal) ||
            !origin.StartsWith("https://", StringComparison.Ordinal) || nonce?.Length != 64 ||
            !nonce.All(Uri.IsHexDigit)) return null;

        using var mutex = AcquireHandoffLock();
        try
        {
            if (!File.Exists(HandoffPath)) return null;
            var handoff = JsonSerializer.Deserialize<Handoff>(File.ReadAllText(HandoffPath));
            if (handoff is null || handoff.InstallationId != installationId || handoff.Origin != origin ||
                handoff.ExpiresAt <= now || handoff.ExpiresAt > now.AddMinutes(5) || handoff.NonceHash?.Length != 64 ||
                !handoff.NonceHash.All(Uri.IsHexDigit) ||
                !CryptographicOperations.FixedTimeEquals(Convert.FromHexString(handoff.NonceHash), Convert.FromHexString(Hash(nonce))))
                return null;

            var consumedPath = HandoffPath + "." + Guid.NewGuid().ToString("N") + ".consumed";
            // The shared OS lock covers creation, validation and the atomic claim across processes.
            File.Move(HandoffPath, consumedPath, overwrite: false);
            File.Delete(consumedPath);
            return options.Security.Token;
        }
        catch (Exception exception) when (exception is IOException or JsonException or FormatException)
        {
            return null;
        }
        finally { mutex.ReleaseMutex(); }
    }

    private Mutex AcquireHandoffLock()
    {
        var mutex = new Mutex(false, MutexName);
        try
        {
            try
            {
                if (!mutex.WaitOne(TimeSpan.FromSeconds(5))) throw new IOException("Connection is busy.");
            }
            catch (AbandonedMutexException) { }
            return mutex;
        }
        catch
        {
            mutex.Dispose();
            throw;
        }
    }

    private static string Hash(string nonce) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(nonce)));
    private sealed record Handoff(string InstallationId, string NonceHash, string Origin, DateTimeOffset ExpiresAt);
    public sealed record ConnectionRequest(string Nonce);
}
