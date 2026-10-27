using System.Text;

namespace BuscaTabelas.Core.Logging;

/// <summary>
/// Grava um arquivo por dia em <c>Logs\yyyy-MM-dd.log</c> e mantém os registros recentes em memória
/// para a tela de logs. Falhas de gravação nunca derrubam o aplicativo.
/// </summary>
public sealed class FileLoggingService : ILoggingService
{
    private const int MaxRecentEntries = 2000;

    private readonly object _sync = new();
    private readonly Queue<LogEntry> _recent = new();

    public FileLoggingService(string logsFolder)
    {
        LogsFolder = logsFolder;
        try
        {
            Directory.CreateDirectory(logsFolder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Sem pasta de logs o aplicativo continua funcionando; os registros ficam só em memória.
        }
    }

    public event EventHandler<LogEntry>? EntryLogged;

    public string LogsFolder { get; }

    public string CurrentLogFilePath => GetFilePath(DateTime.Now);

    public void Log(LogEntry entry)
    {
        lock (_sync)
        {
            _recent.Enqueue(entry);
            if (_recent.Count > MaxRecentEntries)
            {
                _recent.Dequeue();
            }

            try
            {
                File.AppendAllText(GetFilePath(entry.Timestamp), entry.ToFileLine() + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }

        EntryLogged?.Invoke(this, entry);
    }

    public IReadOnlyList<LogEntry> GetRecentEntries()
    {
        lock (_sync)
        {
            return _recent.ToArray();
        }
    }

    private string GetFilePath(DateTime date) => Path.Combine(LogsFolder, $"{date:yyyy-MM-dd}.log");
}
