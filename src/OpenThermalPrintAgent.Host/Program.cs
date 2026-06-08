using System.Net;
using System.Net.WebSockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting.WindowsServices;
using OpenThermalPrintAgent.Core.Errors;
using OpenThermalPrintAgent.Core.Models;
using OpenThermalPrintAgent.Core.Printing;
using OpenThermalPrintAgent.Core.Validation;
using OpenThermalPrintAgent.EscPos;
using OpenThermalPrintAgent.Host;
using OpenThermalPrintAgent.Host.Configuration;
using OpenThermalPrintAgent.Host.Json;
using OpenThermalPrintAgent.Host.Queue;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseWindowsService(options =>
{
    options.ServiceName = "Open Thermal Print Agent";
});

builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection("Agent"));
var agentOptions = builder.Configuration.GetSection("Agent").Get<AgentOptions>() ?? new AgentOptions();

builder.WebHost.ConfigureKestrel(options =>
{
    options.Listen(IPAddress.Loopback, agentOptions.Port);
    options.Limits.MaxRequestBodySize = agentOptions.MaxRequestBodyBytes;
});

builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.Converters.Add(new PaperWidthJsonConverter());
    options.SerializerOptions.Converters.Add(new TextAlignmentJsonConverter());
    options.SerializerOptions.Converters.Add(new PrintCommandTypeJsonConverter());
    options.SerializerOptions.Converters.Add(new CutModeJsonConverter());
    options.SerializerOptions.Converters.Add(new EncodingProfileJsonConverter());
    options.SerializerOptions.Converters.Add(new BarcodeTypeJsonConverter());
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("ConfiguredOrigins", policy =>
    {
        policy.WithOrigins(agentOptions.AllowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddSingleton<EscPosRenderer>();
builder.Services.AddSingleton<PrintJobQueue>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<PrintJobQueue>());
builder.Services.AddPlatformPrinting();

var app = builder.Build();

app.UseCors("ConfiguredOrigins");
app.UseWebSockets();

const string ApiVersion = "v1";
const string ApiPrefix = "/api/v1";

MapEndpoints(app);
MapEndpoints(app.MapGroup(ApiPrefix));

app.Run();

static void MapEndpoints(IEndpointRouteBuilder endpoints)
{
    endpoints.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    name = "open-thermal-print-agent",
    version = GetAgentVersion(),
    agentVersion = GetAgentVersion(),
    apiVersion = ApiVersion,
    platform = RuntimeInformation.OSDescription
}));

    endpoints.MapGet("/printers", (IServiceProvider services) =>
{
    if (!TryGetService<IPrinterProvider>(services, out var printerProvider, out var unsupported))
    {
        return Results.Json(unsupported, statusCode: StatusCodes.Status501NotImplemented);
    }

    try
    {
        return Results.Ok(printerProvider.ListPrinters());
    }
    catch (PlatformNotSupportedException exception)
    {
        return Results.Json(AgentError.UnsupportedPlatform(exception.Message), statusCode: StatusCodes.Status501NotImplemented);
    }
});

    endpoints.MapGet("/jobs/recent", (PrintJobQueue queue) => Results.Ok(queue.GetRecentJobs()));

    endpoints.MapPost("/print/test", (
    HttpRequest httpRequest,
    TestPrintRequest request,
    EscPosRenderer renderer,
    IServiceProvider services,
    IOptions<AgentOptions> options) =>
{
    var tokenError = ValidatePrintToken(httpRequest, options.Value);
    if (tokenError is not null)
    {
        return tokenError;
    }

    var validationError = PrintJobValidator.Validate(request);
    if (validationError is not null)
    {
        return ErrorResult(validationError);
    }

    if (!TryGetService<IPrinterProvider>(services, out var printerProvider, out var unsupported) ||
        !TryGetService<IRawPrinter>(services, out var rawPrinter, out unsupported))
    {
        return Results.Json(unsupported, statusCode: StatusCodes.Status501NotImplemented);
    }

    if (!printerProvider.PrinterExists(request.PrinterName))
    {
        return ErrorResult(AgentError.PrinterNotFound(request.PrinterName));
    }

    try
    {
        var now = DateTimeOffset.UtcNow;
        var bytes = renderer.RenderTestReceipt(request, now);
        rawPrinter.PrintRaw(request.PrinterName, bytes);

        return Results.Ok(new PrintJobResponse
        {
            JobId = Guid.NewGuid().ToString("N"),
            Status = "printed",
            PrinterName = request.PrinterName,
            PrintedAt = now
        });
    }
    catch (EscPosRenderException exception)
    {
        return ErrorResult(exception.Error);
    }
    catch (UnauthorizedAccessException exception)
    {
        return Results.Json(new AgentError { Code = ErrorCodes.AccessDenied, Message = exception.Message }, statusCode: StatusCodes.Status403Forbidden);
    }
    catch (Exception exception) when (exception is InvalidOperationException or ExternalException)
    {
        return ErrorResult(AgentError.PrintFailed(exception.Message));
    }
});

    endpoints.MapPost("/print", (
    HttpRequest httpRequest,
    PrintJobRequest request,
    EscPosRenderer renderer,
    IServiceProvider services,
    IOptions<AgentOptions> options) =>
{
    var tokenError = ValidatePrintToken(httpRequest, options.Value);
    if (tokenError is not null)
    {
        return tokenError;
    }

    var validationError = PrintJobValidator.Validate(request);
    if (validationError is not null)
    {
        return ErrorResult(validationError);
    }

    if (options.Value.Queue.Enabled)
    {
        var queued = services.GetRequiredService<PrintJobQueue>().Enqueue(request);
        return Results.Ok(new PrintJobResponse
        {
            JobId = queued.JobId,
            Status = queued.Status,
            PrinterName = queued.PrinterName,
            PrintedAt = queued.UpdatedAt
        });
    }

    if (!TryGetService<IPrinterProvider>(services, out var printerProvider, out var unsupported) ||
        !TryGetService<IRawPrinter>(services, out var rawPrinter, out unsupported))
    {
        return Results.Json(unsupported, statusCode: StatusCodes.Status501NotImplemented);
    }

    if (!printerProvider.PrinterExists(request.PrinterName))
    {
        return ErrorResult(AgentError.PrinterNotFound(request.PrinterName));
    }

    try
    {
        var now = DateTimeOffset.UtcNow;
        var bytes = renderer.Render(request);
        rawPrinter.PrintRaw(request.PrinterName, bytes);

        return Results.Ok(new PrintJobResponse
        {
            JobId = string.IsNullOrWhiteSpace(request.JobId) ? Guid.NewGuid().ToString("N") : request.JobId,
            Status = "printed",
            PrinterName = request.PrinterName,
            PrintedAt = now
        });
    }
    catch (EscPosRenderException exception)
    {
        return ErrorResult(exception.Error);
    }
    catch (UnauthorizedAccessException exception)
    {
        return Results.Json(new AgentError { Code = ErrorCodes.AccessDenied, Message = exception.Message }, statusCode: StatusCodes.Status403Forbidden);
    }
    catch (Exception exception) when (exception is InvalidOperationException or ExternalException)
    {
        return ErrorResult(AgentError.PrintFailed(exception.Message));
    }
});

    endpoints.Map("/ws", HandleWebSocketAsync);
}

static string GetAgentVersion()
{
    return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.1.0";
}

static IResult? ValidatePrintToken(HttpRequest request, AgentOptions options)
{
    if (!options.Security.RequireToken)
    {
        return null;
    }

    if (string.IsNullOrWhiteSpace(options.Security.Token))
    {
        return Results.Json(
            new AgentError { Code = ErrorCodes.AccessDenied, Message = "Print token is required by configuration but no token is configured." },
            statusCode: StatusCodes.Status403Forbidden);
    }

    var providedToken = GetProvidedToken(request, options.Security.HeaderName);
    if (string.IsNullOrWhiteSpace(providedToken))
    {
        return Results.Json(
            new AgentError { Code = ErrorCodes.AccessDenied, Message = "Print token is required." },
            statusCode: StatusCodes.Status401Unauthorized);
    }

    return string.Equals(providedToken, options.Security.Token, StringComparison.Ordinal)
        ? null
        : Results.Json(
            new AgentError { Code = ErrorCodes.AccessDenied, Message = "Print token is invalid." },
            statusCode: StatusCodes.Status403Forbidden);
}

static string? GetProvidedToken(HttpRequest request, string headerName)
{
    if (request.Query.TryGetValue("token", out var queryToken))
    {
        return queryToken.ToString();
    }

    if (request.Headers.TryGetValue(headerName, out var headerToken))
    {
        return headerToken.ToString();
    }

    var authorization = request.Headers.Authorization.ToString();
    const string bearerPrefix = "Bearer ";
    return authorization.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase)
        ? authorization[bearerPrefix.Length..].Trim()
        : null;
}

static async Task HandleWebSocketAsync(
    HttpContext context,
    EscPosRenderer renderer,
    IServiceProvider services,
    IOptions<AgentOptions> options,
    IOptions<JsonOptions> jsonOptions)
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    var tokenError = ValidatePrintToken(context.Request, options.Value);
    if (tokenError is not null)
    {
        await tokenError.ExecuteAsync(context);
        return;
    }

    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
    while (webSocket.State == WebSocketState.Open)
    {
        var message = await ReceiveTextMessageAsync(webSocket, options.Value.MaxRequestBodyBytes, context.RequestAborted);
        if (message is null)
        {
            break;
        }

        var response = await HandleWebSocketMessageAsync(message, renderer, services, jsonOptions.Value.SerializerOptions);
        await SendJsonMessageAsync(webSocket, response, jsonOptions.Value.SerializerOptions, context.RequestAborted);
    }
}

static async Task<object> HandleWebSocketMessageAsync(
    string message,
    EscPosRenderer renderer,
    IServiceProvider services,
    JsonSerializerOptions jsonOptions)
{
    WebSocketRequest? request;
    try
    {
        request = JsonSerializer.Deserialize<WebSocketRequest>(message, jsonOptions);
    }
    catch (JsonException exception)
    {
        return WebSocketError("invalid_message", exception.Message);
    }

    return request?.Type switch
    {
        "health" => new
        {
            type = "health",
            status = "ok",
            name = "open-thermal-print-agent",
            agentVersion = GetAgentVersion(),
            apiVersion = ApiVersion,
            platform = RuntimeInformation.OSDescription
        },
        "print" => await HandleWebSocketPrintAsync(request.Payload, renderer, services, jsonOptions),
        _ => WebSocketError("unsupported_message_type", "Supported WebSocket message types are health and print.")
    };
}

static Task<object> HandleWebSocketPrintAsync(
    JsonElement? payload,
    EscPosRenderer renderer,
    IServiceProvider services,
    JsonSerializerOptions jsonOptions)
{
    if (payload is null)
    {
        return Task.FromResult<object>(WebSocketError("invalid_payload", "payload is required for print messages."));
    }

    PrintJobRequest? request;
    try
    {
        request = payload.Value.Deserialize<PrintJobRequest>(jsonOptions);
    }
    catch (JsonException exception)
    {
        return Task.FromResult<object>(WebSocketError("invalid_payload", exception.Message));
    }

    if (request is null)
    {
        return Task.FromResult<object>(WebSocketError("invalid_payload", "payload is required for print messages."));
    }

    var validationError = PrintJobValidator.Validate(request);
    if (validationError is not null)
    {
        return Task.FromResult<object>(WebSocketError(validationError.Code, validationError.Message, validationError.Details));
    }

    if (!TryGetService<IPrinterProvider>(services, out var printerProvider, out var unsupported) ||
        !TryGetService<IRawPrinter>(services, out var rawPrinter, out unsupported))
    {
        return Task.FromResult<object>(WebSocketError(unsupported.Code, unsupported.Message, unsupported.Details));
    }

    if (!printerProvider.PrinterExists(request.PrinterName))
    {
        var error = AgentError.PrinterNotFound(request.PrinterName);
        return Task.FromResult<object>(WebSocketError(error.Code, error.Message, error.Details));
    }

    try
    {
        var now = DateTimeOffset.UtcNow;
        var bytes = renderer.Render(request);
        rawPrinter.PrintRaw(request.PrinterName, bytes);

        return Task.FromResult<object>(new
        {
            type = "printResult",
            jobId = string.IsNullOrWhiteSpace(request.JobId) ? Guid.NewGuid().ToString("N") : request.JobId,
            status = "printed",
            printerName = request.PrinterName,
            printedAt = now
        });
    }
    catch (EscPosRenderException exception)
    {
        return Task.FromResult<object>(WebSocketError(exception.Error.Code, exception.Error.Message, exception.Error.Details));
    }
    catch (Exception exception) when (exception is InvalidOperationException or ExternalException or UnauthorizedAccessException)
    {
        var error = exception is UnauthorizedAccessException
            ? new AgentError { Code = ErrorCodes.AccessDenied, Message = exception.Message }
            : AgentError.PrintFailed(exception.Message);
        return Task.FromResult<object>(WebSocketError(error.Code, error.Message, error.Details));
    }
}

static async Task<string?> ReceiveTextMessageAsync(WebSocket webSocket, long maxBytes, CancellationToken cancellationToken)
{
    var buffer = new byte[4096];
    using var output = new MemoryStream();

    while (true)
    {
        WebSocketReceiveResult result;
        try
        {
            result = await webSocket.ReceiveAsync(buffer, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException or WebSocketException)
        {
            return null;
        }

        if (result.MessageType == WebSocketMessageType.Close)
        {
            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by client.", cancellationToken);
            return null;
        }

        if (result.MessageType != WebSocketMessageType.Text)
        {
            return null;
        }

        output.Write(buffer.AsSpan(0, result.Count));
        if (output.Length > maxBytes)
        {
            return JsonSerializer.Serialize(WebSocketError("message_too_large", "WebSocket message exceeded the configured size limit."));
        }

        if (result.EndOfMessage)
        {
            return Encoding.UTF8.GetString(output.ToArray());
        }
    }
}

static async Task SendJsonMessageAsync(WebSocket webSocket, object response, JsonSerializerOptions jsonOptions, CancellationToken cancellationToken)
{
    var json = JsonSerializer.Serialize(response, jsonOptions);
    var bytes = Encoding.UTF8.GetBytes(json);
    await webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
}

static object WebSocketError(string code, string message, IReadOnlyList<string>? details = null) => new
{
    type = "error",
    code,
    message,
    details = details ?? []
};

static bool TryGetService<T>(IServiceProvider services, out T service, out AgentError unsupported)
    where T : class
{
    service = services.GetService<T>()!;
    unsupported = AgentError.UnsupportedPlatform(RuntimeInformation.OSDescription);
    return service is not null;
}

static IResult ErrorResult(AgentError error)
{
    var statusCode = error.Code switch
    {
        ErrorCodes.PrinterNotFound => StatusCodes.Status404NotFound,
        ErrorCodes.UnsupportedFormat => StatusCodes.Status400BadRequest,
        ErrorCodes.InvalidPayload => StatusCodes.Status400BadRequest,
        ErrorCodes.AccessDenied => StatusCodes.Status403Forbidden,
        ErrorCodes.UnsupportedPlatform => StatusCodes.Status501NotImplemented,
        _ => StatusCodes.Status500InternalServerError
    };

    return Results.Json(error, statusCode: statusCode);
}

internal sealed record WebSocketRequest(string Type, JsonElement? Payload);

public partial class Program;
