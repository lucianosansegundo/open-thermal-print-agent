namespace OpenThermalPrintAgent.Host;

internal sealed class InstalledLogProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new FailureLogger(categoryName);
    public void Dispose() { }

    private sealed class FailureLogger(string category) : ILogger
    {
        private static readonly object Sync = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => level >= LogLevel.Warning;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            var directory = Path.Combine(InstalledConfiguration.DirectoryPath, "logs");
            Directory.CreateDirectory(directory);
            // Category, event and exception type permit diagnosis without request URLs, tokens or receipt content.
            var entry = $"{DateTimeOffset.UtcNow:O} {logLevel} {category} event={eventId.Id} exception={exception?.GetType().Name ?? "none"}\n";
            lock (Sync) File.AppendAllText(Path.Combine(directory, DateTime.UtcNow.ToString("yyyy-MM-dd") + ".log"), entry);
        }
    }
}
