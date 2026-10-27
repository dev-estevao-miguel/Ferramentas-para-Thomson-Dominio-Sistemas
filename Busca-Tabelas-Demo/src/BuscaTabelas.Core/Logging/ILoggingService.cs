namespace BuscaTabelas.Core.Logging;

public interface ILoggingService
{
    /// <summary>Disparado a cada novo registro, em qualquer thread.</summary>
    event EventHandler<LogEntry>? EntryLogged;

    string LogsFolder { get; }

    string CurrentLogFilePath { get; }

    void Log(LogEntry entry);

    IReadOnlyList<LogEntry> GetRecentEntries();

    void Log(LogLevel level, string message, Exception? exception = null) =>
        Log(new LogEntry(DateTime.Now, level, message, exception?.ToString()));

    void Debug(string message) => Log(LogLevel.Debug, message);

    void Information(string message) => Log(LogLevel.Information, message);

    void Warning(string message, Exception? exception = null) => Log(LogLevel.Warning, message, exception);

    void Error(string message, Exception? exception = null) => Log(LogLevel.Error, message, exception);
}
