namespace OpenThermalPrintAgent.Core.Errors;

public sealed record AgentError
{
    public required string Code { get; init; }

    public required string Message { get; init; }

    public IReadOnlyList<string> Details { get; init; } = [];

    public static AgentError InvalidPayload(params string[] details) => new()
    {
        Code = ErrorCodes.InvalidPayload,
        Message = "The print payload is invalid.",
        Details = details
    };

    public static AgentError UnsupportedFormat(string format) => new()
    {
        Code = ErrorCodes.UnsupportedFormat,
        Message = $"Unsupported print format: {format}."
    };

    public static AgentError PrinterNotFound(string printerName) => new()
    {
        Code = ErrorCodes.PrinterNotFound,
        Message = $"Printer was not found: {printerName}."
    };

    public static AgentError PrintFailed(string message) => new()
    {
        Code = ErrorCodes.PrintFailed,
        Message = message
    };

    public static AgentError UnsupportedPlatform(string platform) => new()
    {
        Code = ErrorCodes.UnsupportedPlatform,
        Message = $"Unsupported platform: {platform}."
    };
}
