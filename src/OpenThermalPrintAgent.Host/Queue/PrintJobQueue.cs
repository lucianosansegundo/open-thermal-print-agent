using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using OpenThermalPrintAgent.Core.Errors;
using OpenThermalPrintAgent.Core.Models;
using OpenThermalPrintAgent.Core.Printing;
using OpenThermalPrintAgent.EscPos;
using OpenThermalPrintAgent.Host.Configuration;

namespace OpenThermalPrintAgent.Host.Queue;

public sealed class PrintJobQueue : BackgroundService
{
    private readonly Channel<PrintJobRequest> _channel = Channel.CreateUnbounded<PrintJobRequest>();
    private readonly ConcurrentDictionary<string, QueuedPrintJobRecord> _records = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<string> _recentJobIds = new();
    private readonly EscPosRenderer _renderer;
    private readonly IServiceProvider _services;
    private readonly AgentQueueOptions _options;
    private readonly ILogger<PrintJobQueue> _logger;

    public PrintJobQueue(
        EscPosRenderer renderer,
        IServiceProvider services,
        IOptions<AgentOptions> options,
        ILogger<PrintJobQueue> logger)
    {
        _renderer = renderer;
        _services = services;
        _options = options.Value.Queue;
        _logger = logger;
    }

    public QueuedPrintJobRecord Enqueue(PrintJobRequest request)
    {
        var jobId = string.IsNullOrWhiteSpace(request.JobId) ? Guid.NewGuid().ToString("N") : request.JobId;
        var queuedRequest = request with { JobId = jobId };
        var now = DateTimeOffset.UtcNow;
        var record = new QueuedPrintJobRecord
        {
            JobId = jobId,
            PrinterName = request.PrinterName,
            Status = "queued",
            CreatedAt = now,
            UpdatedAt = now,
            Request = queuedRequest
        };

        Store(record);
        _channel.Writer.TryWrite(queuedRequest);
        return record;
    }

    public IReadOnlyList<QueuedPrintJobRecord> GetRecentJobs()
    {
        return _recentJobIds
            .Reverse()
            .Select(jobId => _records.TryGetValue(jobId, out var record) ? record with { Request = null } : null)
            .Where(record => record is not null)
            .Cast<QueuedPrintJobRecord>()
            .ToArray();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var request in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            await ProcessAsync(request, stoppingToken);
        }
    }

    private async Task ProcessAsync(PrintJobRequest request, CancellationToken stoppingToken)
    {
        var maxAttempts = Math.Max(1, _options.RetryAttempts + 1);
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            Store(Update(request, "printing", attempt));

            try
            {
                if (!TryGetService<IPrinterProvider>(out var printerProvider) ||
                    !TryGetService<IRawPrinter>(out var rawPrinter))
                {
                    throw new PlatformNotSupportedException("Printing services are not available on this platform.");
                }

                if (!printerProvider.PrinterExists(request.PrinterName))
                {
                    throw new InvalidOperationException(AgentError.PrinterNotFound(request.PrinterName).Message);
                }

                var bytes = _renderer.Render(request);
                rawPrinter.PrintRaw(request.PrinterName, bytes);
                Store(Update(request, "printed", attempt));
                return;
            }
            catch (Exception exception) when (exception is InvalidOperationException or PlatformNotSupportedException or UnauthorizedAccessException or System.Runtime.InteropServices.ExternalException)
            {
                _logger.LogWarning(exception, "Queued print job {JobId} failed on attempt {Attempt}.", request.JobId, attempt);
                if (attempt == maxAttempts)
                {
                    Store(Update(request, "failed", attempt, ErrorCodes.PrintFailed, exception.Message));
                    return;
                }

                await Task.Delay(Math.Max(0, _options.RetryDelayMilliseconds), stoppingToken);
            }
        }
    }

    private bool TryGetService<T>(out T service)
        where T : class
    {
        service = _services.GetService<T>()!;
        return service is not null;
    }

    private QueuedPrintJobRecord Update(PrintJobRequest request, string status, int attempts, string? errorCode = null, string? errorMessage = null)
    {
        var existing = _records.TryGetValue(request.JobId ?? string.Empty, out var record) ? record : null;
        return new QueuedPrintJobRecord
        {
            JobId = request.JobId ?? Guid.NewGuid().ToString("N"),
            PrinterName = request.PrinterName,
            Status = status,
            Attempts = attempts,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            CreatedAt = existing?.CreatedAt ?? DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Request = request
        };
    }

    private void Store(QueuedPrintJobRecord record)
    {
        _records[record.JobId] = record;
        _recentJobIds.Enqueue(record.JobId);

        while (_recentJobIds.Count > Math.Max(1, _options.MaxRecentJobs) && _recentJobIds.TryDequeue(out var oldJobId))
        {
            _records.TryRemove(oldJobId, out _);
        }
    }
}
