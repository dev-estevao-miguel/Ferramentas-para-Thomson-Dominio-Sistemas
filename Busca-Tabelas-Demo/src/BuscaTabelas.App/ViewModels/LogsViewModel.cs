using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BuscaTabelas.App.Services;
using BuscaTabelas.Core.Logging;

namespace BuscaTabelas.App.ViewModels;

public enum LogFilter
{
    All,
    WarningsAndErrors,
    ErrorsOnly
}

public sealed partial class LogsViewModel : PageViewModel
{
    private const int MaxEntries = 2000;

    private readonly ILoggingService _logger;
    private readonly IShellService _shell;
    private readonly List<LogEntry> _allEntries;

    [ObservableProperty]
    private LogFilter _filter = LogFilter.All;

    public LogsViewModel(ILoggingService logger, IShellService shell, IUiDispatcher dispatcher)
    {
        _logger = logger;
        _shell = shell;
        _allEntries = logger.GetRecentEntries().ToList();
        ApplyFilter();

        logger.EntryLogged += (_, entry) => dispatcher.Post(() => Append(entry));
    }

    public override string Title => "Logs";

    public ObservableCollection<LogEntry> Entries { get; } = [];

    public string LogFilePath => _logger.CurrentLogFilePath;

    partial void OnFilterChanged(LogFilter value) => ApplyFilter();

    [RelayCommand]
    private void OpenLogsFolder() => _shell.OpenFolder(_logger.LogsFolder);

    [RelayCommand]
    private void CopyVisible() =>
        _shell.CopyToClipboard(string.Join(Environment.NewLine, Entries.Select(e => e.ToFileLine())));

    [RelayCommand]
    private void ClearView()
    {
        _allEntries.Clear();
        Entries.Clear();
    }

    private void Append(LogEntry entry)
    {
        _allEntries.Add(entry);
        if (_allEntries.Count > MaxEntries)
        {
            _allEntries.RemoveAt(0);
        }

        if (Matches(entry))
        {
            Entries.Add(entry);
            if (Entries.Count > MaxEntries)
            {
                Entries.RemoveAt(0);
            }
        }
    }

    private void ApplyFilter()
    {
        Entries.Clear();
        foreach (var entry in _allEntries.Where(Matches))
        {
            Entries.Add(entry);
        }
    }

    private bool Matches(LogEntry entry) => Filter switch
    {
        LogFilter.WarningsAndErrors => entry.Level >= LogLevel.Warning,
        LogFilter.ErrorsOnly => entry.Level == LogLevel.Error,
        _ => true
    };
}
