using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using OpenThermalPrintAgent.Host.Configuration;

namespace OpenThermalPrintAgent.Host.Tests;

public sealed class InstalledConfigurationTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "thermal-agent-tests-" + Guid.NewGuid().ToString("N"));
    private string ConfigPath => Path.Combine(directory, "agent.json");

    [Fact]
    public void InstallationCreatesTokenAndExplicitOriginWithoutDevelopmentOrigins()
    {
        var config = InstalledConfiguration.Ensure(ConfigPath, "https://pos.example");
        Assert.Equal(64, config["Agent"]!["Security"]!["Token"]!.GetValue<string>().Length);
        var configuration = new ConfigurationBuilder().AddJsonFile(ConfigPath).Build();
        var options = configuration.GetSection("Agent").Get<AgentOptions>()!;
        Assert.Equal(new[] { "https://pos.example" }, options.AllowedOrigins);
        Assert.True(options.Security.RequireToken);
        Assert.False(options.Queue.Enabled);
        Assert.Contains(options.Security.Token!, File.ReadAllText(Path.Combine(directory, "setup.html")));
    }

    [Fact]
    public void UpgradeAndReinstallPreserveConfigurationAndTokenExactly()
    {
        InstalledConfiguration.Ensure(ConfigPath, "https://pos.example");
        var original = File.ReadAllText(ConfigPath);
        InstalledConfiguration.Ensure(ConfigPath, "https://other.example");
        Assert.Equal(original, File.ReadAllText(ConfigPath));
    }

    [Fact]
    public void InvalidExistingConfigurationIsPreservedAndRejected()
    {
        var config = InstalledConfiguration.Ensure(ConfigPath, "https://pos.example");
        config["Agent"]!["Security"]!["RequireToken"] = false;
        File.WriteAllText(ConfigPath, config.ToJsonString());
        var original = File.ReadAllText(ConfigPath);
        Assert.Throws<InvalidDataException>(() => InstalledConfiguration.Ensure(ConfigPath, "https://pos.example"));
        Assert.Equal(original, File.ReadAllText(ConfigPath));
    }

    [Theory]
    [InlineData("*")]
    [InlineData("https://*.example")]
    [InlineData("https://pos.example/path")]
    [InlineData("https://pos.example?token=x")]
    [InlineData("https://user:pass@pos.example")]
    [InlineData("file:///C:/private")]
    public void RejectsNonOriginConfiguration(string origin)
    {
        Assert.Throws<ArgumentException>(() => InstalledConfiguration.Ensure(ConfigPath, origin));
        Assert.False(File.Exists(ConfigPath));
    }

    [Fact]
    public void SeparateInstallationsHaveIndependentTokens()
    {
        var first = InstalledConfiguration.Ensure(ConfigPath, "https://pos.example");
        var second = InstalledConfiguration.Ensure(Path.Combine(directory, "other", "agent.json"), "https://pos.example");
        Assert.NotEqual(first["Agent"]!["Security"]!["Token"]!.GetValue<string>(), second["Agent"]!["Security"]!["Token"]!.GetValue<string>());
    }

    [Fact]
    public async Task InstalledStartupRejectsSecondInstanceAndRespondsToOwnedStopSignal()
    {
        if (!OperatingSystem.IsWindows()) return;
        var name = @"Local\ThermalAgentTest-" + Guid.NewGuid().ToString("N");
        var app = WebApplication.CreateBuilder().Build();
        using (var runtime = new InstalledRuntime(app, name, directory))
        {
            Exception? duplicateError = null;
            var duplicate = new Thread(() =>
            {
                try { using var second = new InstalledRuntime(app, name, directory); }
                catch (Exception exception) { duplicateError = exception; }
            });
            duplicate.Start();
            Assert.True(duplicate.Join(TimeSpan.FromSeconds(5)));
            Assert.IsType<InvalidOperationException>(duplicateError);
            using var stop = EventWaitHandle.OpenExisting(name + "-stop");
            stop.Set();
            Assert.True(app.Lifetime.ApplicationStopping.WaitHandle.WaitOne(TimeSpan.FromSeconds(5)));
        }
        await app.DisposeAsync();
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}
