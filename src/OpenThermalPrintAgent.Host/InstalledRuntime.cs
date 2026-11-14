using System.Security.Principal;
using System.Diagnostics;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using OpenThermalPrintAgent.Host.Configuration;

namespace OpenThermalPrintAgent.Host;

internal sealed class InstalledRuntime : IDisposable
{
    private readonly Mutex mutex;
    private readonly EventWaitHandle stop;
    private readonly RegisteredWaitHandle stopRegistration;
    private readonly string logPath;

    private static string ObjectName
    {
        get
        {
            if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
            using var identity = WindowsIdentity.GetCurrent();
            return @"Local\OpenThermalPrintAgent-" + identity.User!.Value;
        }
    }

    public InstalledRuntime(WebApplication app, string? instanceName = null, string? logDirectory = null)
    {
        instanceName ??= ObjectName;
        mutex = new Mutex(false, instanceName);
        bool acquired;
        try { acquired = mutex.WaitOne(0); }
        catch (AbandonedMutexException) { acquired = true; }
        if (!acquired)
        {
            mutex.Dispose();
            throw new InvalidOperationException("The installed agent is already running for this Windows user.");
        }
        stop = new EventWaitHandle(false, EventResetMode.ManualReset, instanceName + "-stop");
        stop.Reset();
        stopRegistration = ThreadPool.RegisterWaitForSingleObject(stop, (_, _) => app.Lifetime.StopApplication(), null, Timeout.Infinite, true);
        logDirectory ??= Path.Combine(InstalledConfiguration.DirectoryPath, "logs");
        logPath = Path.Combine(logDirectory, DateTime.UtcNow.ToString("yyyy-MM-dd") + ".log");
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        Log("Agent starting.");
    }

    public static int Command(string[] args)
    {
        try
        {
            if (args[0] == "--configure")
            {
                InstalledConfiguration.Ensure(InstalledConfiguration.ConfigPath, args[1]);
                return 0;
            }
            if (args[0] == "--connect")
            {
                // Verify the listener belongs to this Windows user's installation before issuing a handoff.
                if (Command(["--check"]) != 0) return 1;
                var configuration = new ConfigurationBuilder().AddJsonFile(InstalledConfiguration.ConfigPath).Build();
                var options = configuration.GetSection("Agent").Get<AgentOptions>()!;
                var connection = new LocalConnection(InstalledConfiguration.DirectoryPath, configuration["InstallationId"], options);
                var url = connection.Prepare(DateTimeOffset.UtcNow);
                if (!args.Contains("--no-open")) Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                return 0;
            }
            if (args[0] == "--stop")
            {
                if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
                if (EventWaitHandle.TryOpenExisting(ObjectName + "-stop", out var stopEvent))
                {
                    using (stopEvent) stopEvent.Set();
                }
                using var instance = new Mutex(false, ObjectName);
                try
                {
                    if (!instance.WaitOne(TimeSpan.FromSeconds(30))) return 2;
                }
                catch (AbandonedMutexException) { }
                instance.ReleaseMutex();
                Thread.Sleep(250);
                return 0;
            }
            if (args[0] == "--check")
            {
                var config = JsonNode.Parse(File.ReadAllText(InstalledConfiguration.ConfigPath))!.AsObject();
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
                for (var attempt = 0; attempt < 15; attempt++)
                {
                    try
                    {
                        var health = JsonNode.Parse(http.GetStringAsync("http://127.0.0.1:17890/api/v1/health").GetAwaiter().GetResult());
                        if (health?["installationId"]?.GetValue<string>() == config["InstallationId"]!.GetValue<string>()) return 0;
                    }
                    catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException) { }
                    Thread.Sleep(1000);
                }
                throw new InvalidOperationException("Installation health check failed. Check the local logs and whether port 17890 is occupied.");
            }
            return 1;
        }
        catch (Exception exception)
        {
            LogFailure(exception);
            return 1;
        }
    }

    public static void LogFailure(Exception exception)
    {
        var directory = Path.Combine(InstalledConfiguration.DirectoryPath, "logs");
        Directory.CreateDirectory(directory);
        // Messages may contain configuration values or URLs with tokens. Persist type and a generic diagnosis only.
        File.AppendAllText(Path.Combine(directory, "startup-errors.log"), $"{DateTimeOffset.UtcNow:O} {exception.GetType().Name}: Agent operation failed. Verify agent.json, port 17890 availability and file permissions.\n");
    }

    private void Log(string message) => File.AppendAllText(logPath, $"{DateTimeOffset.UtcNow:O} {message}\n");

    public void Dispose()
    {
        stopRegistration.Unregister(null);
        stop.Dispose();
        Log("Agent stopped.");
        mutex.ReleaseMutex();
        mutex.Dispose();
    }
}
