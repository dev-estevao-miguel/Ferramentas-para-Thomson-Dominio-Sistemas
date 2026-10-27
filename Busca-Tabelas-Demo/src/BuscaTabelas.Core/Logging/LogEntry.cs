namespace BuscaTabelas.Core.Logging;

public enum LogLevel
{
    Debug,
    Information,
    Warning,
    Error
}

public sealed record LogEntry(DateTime Timestamp, LogLevel Level, string Message, string? ExceptionDetail = null)
{
    public string LevelTag => Level switch
    {
        LogLevel.Debug => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        _ => "???"
    };

    public string ToDisplayLine() => $"{Timestamp:HH:mm:ss} {Message}";

    public string ToFileLine()
    {
        var line = $"{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{LevelTag}] {Message}";
        return ExceptionDetail is null ? line : line + Environment.NewLine + ExceptionDetail;
    }
}
