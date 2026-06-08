namespace OpenThermalPrintAgent.Core.Errors;

public static class ErrorCodes
{
    public const string PrinterNotFound = "printer_not_found";
    public const string UnsupportedFormat = "unsupported_format";
    public const string InvalidPayload = "invalid_payload";
    public const string PrintFailed = "print_failed";
    public const string AccessDenied = "access_denied";
    public const string UnsupportedPlatform = "unsupported_platform";
}
