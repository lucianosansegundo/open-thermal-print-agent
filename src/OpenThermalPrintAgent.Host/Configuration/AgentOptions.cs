namespace OpenThermalPrintAgent.Host.Configuration;

public sealed class AgentOptions
{
    public int Port { get; init; } = 17890;

    public string[] AllowedOrigins { get; init; } =
    [
        "http://localhost:5173",
        "https://localhost:5173"
    ];

    public long MaxRequestBodyBytes { get; init; } = 64 * 1024;

    public AgentSecurityOptions Security { get; init; } = new();

    public AgentQueueOptions Queue { get; init; } = new();
}

public sealed class AgentSecurityOptions
{
    public bool RequireToken { get; init; }

    public string? Token { get; init; }

    public string HeaderName { get; init; } = "X-OpenThermalPrintAgent-Token";
}

public sealed class AgentQueueOptions
{
    public bool Enabled { get; init; }

    public int RetryAttempts { get; init; } = 2;

    public int RetryDelayMilliseconds { get; init; } = 500;

    public int MaxRecentJobs { get; init; } = 100;
}
