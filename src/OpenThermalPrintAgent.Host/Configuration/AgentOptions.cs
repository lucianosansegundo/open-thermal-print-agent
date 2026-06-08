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
}
