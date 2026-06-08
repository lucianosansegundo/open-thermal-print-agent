using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Http.Json;
using OpenThermalPrintAgent.Core.Errors;
using OpenThermalPrintAgent.Core.Models;
using OpenThermalPrintAgent.Core.Printing;
using OpenThermalPrintAgent.Core.Validation;
using OpenThermalPrintAgent.EscPos;
using OpenThermalPrintAgent.Host;
using OpenThermalPrintAgent.Host.Configuration;
using OpenThermalPrintAgent.Host.Json;

var builder = WebApplication.CreateBuilder(args);
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
builder.Services.AddPlatformPrinting();

var app = builder.Build();

app.UseCors("ConfiguredOrigins");

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    name = "open-thermal-print-agent",
    version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.1.0",
    platform = RuntimeInformation.OSDescription
}));

app.MapGet("/printers", (IServiceProvider services) =>
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

app.MapPost("/print/test", (
    TestPrintRequest request,
    EscPosRenderer renderer,
    IServiceProvider services) =>
{
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

app.MapPost("/print", (
    PrintJobRequest request,
    EscPosRenderer renderer,
    IServiceProvider services) =>
{
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

app.Run();

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
