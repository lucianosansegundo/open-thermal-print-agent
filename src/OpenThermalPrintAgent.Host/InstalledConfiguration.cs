using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OpenThermalPrintAgent.Host;

public static class InstalledConfiguration
{
    public static string DirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenThermalPrintAgent");
    public static string ConfigPath => Path.Combine(DirectoryPath, "agent.json");

    public static void ValidateOrigin(string origin)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "https" && uri.Scheme != "http") ||
            !string.IsNullOrEmpty(uri.UserInfo) || uri.AbsolutePath != "/" ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
            origin.Contains('*') || origin.TrimEnd('/') != uri.GetLeftPart(UriPartial.Authority))
        {
            throw new ArgumentException("Enter an exact HTTP(S) site origin, without a path or wildcard.");
        }
    }

    public static JsonObject Ensure(string path, string origin)
    {
        ValidateOrigin(origin);
        if (File.Exists(path))
        {
            var existing = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            Validate(existing);
            WriteSetup(path, existing);
            return existing;
        }

        var config = new JsonObject
        {
            ["InstallationId"] = Guid.NewGuid().ToString("N"),
            ["Agent"] = new JsonObject
            {
                ["Port"] = 17890,
                ["AllowedOrigins"] = new JsonArray(origin.TrimEnd('/')),
                ["Security"] = new JsonObject
                {
                    ["RequireToken"] = true,
                    ["Token"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
                    ["HeaderName"] = "X-OpenThermalPrintAgent-Token"
                },
                ["Queue"] = new JsonObject { ["Enabled"] = false }
            }
        };
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, config, new JsonSerializerOptions { WriteIndented = true });
                stream.Flush(flushToDisk: true);
            }
            // Atomic publication preserves an existing token through interruption or concurrent setup.
            try { File.Move(temporaryPath, path, overwrite: false); }
            catch (IOException) when (File.Exists(path))
            {
                config = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
                Validate(config);
            }
        }
        finally { File.Delete(temporaryPath); }
        WriteSetup(path, config);
        return config;
    }

    public static void Validate(JsonObject config)
    {
        var agent = config["Agent"]!.AsObject();
        if (string.IsNullOrWhiteSpace(config["InstallationId"]?.GetValue<string>()) ||
            agent["Port"]?.GetValue<int>() != 17890 ||
            agent["Security"]?["RequireToken"]?.GetValue<bool>() != true ||
            string.IsNullOrWhiteSpace(agent["Security"]?["Token"]?.GetValue<string>()) ||
            agent["Queue"]?["Enabled"]?.GetValue<bool>() != false ||
            agent["AllowedOrigins"] is not JsonArray origins || origins.Count == 0)
        {
            throw new InvalidDataException("Installed configuration requires an installation ID, token, explicit origins and disabled queue retries.");
        }
        foreach (var origin in origins) ValidateOrigin(origin!.GetValue<string>());
    }

    private static void WriteSetup(string path, JsonObject config)
    {
        var token = WebUtility.HtmlEncode(config["Agent"]!["Security"]!["Token"]!.GetValue<string>());
        var origins = WebUtility.HtmlEncode(string.Join(", ", config["Agent"]!["AllowedOrigins"]!.AsArray().Select(x => x!.GetValue<string>())));
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(path)!, "setup.html"), $$"""
            <!doctype html><html lang="en"><meta charset="utf-8"><title>Open Thermal Print Agent setup</title>
            <style>body{font:18px system-ui;max-width:850px;margin:3em auto;padding:1em}input{width:100%;font:16px monospace;padding:10px}</style>
            <h1>Open Thermal Print Agent</h1><p>Agent URL <code>http://127.0.0.1:17890</code></p>
            <p>Allowed site origins {{origins}}</p><label for="token">Local token — copy into your POS printer settings</label>
            <input id="token" readonly value="{{token}}" onclick="this.select()">
            <p>This token permits local printing. Keep this file private to your Windows account.</p>
            <p><a href="http://127.0.0.1:17890/api/v1/health">Check agent health</a></p>
            <p>The agent starts when this Windows user signs in. Select the installed Windows printer in your POS and print a test receipt.</p>
            </html>
            """);
    }
}
