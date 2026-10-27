using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using BancoPronto.App.Services;
using BancoPronto.Core.Abstractions;
using BancoPronto.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BancoPronto.App.ViewModels;

/// <summary>
/// Tela única da demo: Selecionar pasta → Verificação → Preparação → Banco pronto.
/// A lógica fica no Core; aqui só há estado de tela e comandos.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("pt-BR");

    private readonly IBackupScanner _scanner;
    private readonly IPreparationService _preparation;
    private readonly IFolderPicker _folderPicker;
    private readonly IShellService _shell;
    private readonly IThemeService _theme;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FolderDisplay))]
    private string? _folderPath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasScan), nameof(ScanSummary))]
    [NotifyCanExecuteChangedFor(nameof(PrepareCommand))]
    private BackupScanResult? _scanResult;

    [ObservableProperty]
    private IReadOnlyList<BackupItemViewModel> _backups = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle), nameof(ShowProgress))]
    [NotifyCanExecuteChangedFor(nameof(SelectFolderCommand), nameof(UseDemoDataCommand), nameof(PrepareCommand))]
    private bool _isPreparing;

    [ObservableProperty]
    private int _progressPercent;

    [ObservableProperty]
    private string _statusMessage = "Selecione a pasta dos backups ou use os dados de demonstração.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSuccess), nameof(HasFailure), nameof(ShowProgress), nameof(ResultChecks),
        nameof(ResultProblems), nameof(ResultDetail), nameof(DatabasePath))]
    private PreparationResult? _result;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThemeToggleText))]
    private bool _isDarkTheme;

    public MainViewModel(
        IBackupScanner scanner,
        IPreparationService preparation,
        IFolderPicker folderPicker,
        IShellService shell,
        IThemeService theme)
    {
        _scanner = scanner;
        _preparation = preparation;
        _folderPicker = folderPicker;
        _shell = shell;
        _theme = theme;

        Steps =
        [
            new StepItemViewModel(1, "Pasta"),
            new StepItemViewModel(2, "Verificação"),
            new StepItemViewModel(3, "Preparação"),
            new StepItemViewModel(4, "Banco pronto")
        ];
        Steps[0].State = StepState.Active;
    }

    public IReadOnlyList<StepItemViewModel> Steps { get; }

    public ObservableCollection<LogLine> Log { get; } = new();

    public bool IsIdle => !IsPreparing;

    public bool HasScan => ScanResult is not null;

    public bool ShowProgress => IsPreparing || Result is not null;

    public bool HasSuccess => Result is { Succeeded: true };

    public bool HasFailure => Result is { Succeeded: false };

    public string FolderDisplay => FolderPath ?? "Nenhuma pasta selecionada";

    public string ScanSummary => ScanResult is not { } scan
        ? string.Empty
        : $"{Plural(scan.Sequence.Count, "arquivo a aplicar", "arquivos a aplicar")} · {Plural(scan.Ignored.Count, "ignorado", "ignorados")}";

    public IReadOnlyList<string> ResultChecks => Result?.Validation?.Checks ?? Array.Empty<string>();

    public IReadOnlyList<string> ResultProblems
    {
        get
        {
            if (Result is null || Result.Succeeded)
            {
                return Array.Empty<string>();
            }

            if (Result.Validation is { Problems.Count: > 0 } validation)
            {
                return validation.Problems;
            }

            return Result.Error is null ? Array.Empty<string>() : new[] { Result.Error };
        }
    }

    public string ResultDetail => Result is null
        ? string.Empty
        : $"Concluído em {Result.Duration.TotalSeconds.ToString("0.0", Culture)} s. Os backups originais não foram alterados.";

    public string DatabasePath => Result?.DatabasePath ?? string.Empty;

    public string ThemeToggleText => IsDarkTheme ? "Tema claro" : "Tema escuro";

    public string VersionText => $"Versão {typeof(MainViewModel).Assembly.GetName().Version?.ToString(3)} · demo";

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private void SelectFolder()
    {
        var folder = _folderPicker.PickFolder(FolderPath);
        if (folder is not null)
        {
            LoadFolder(folder);
        }
    }

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private void UseDemoData()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "samples", "backups-demo");
        if (!Directory.Exists(folder))
        {
            StatusMessage = "Os arquivos de demonstração não foram encontrados ao lado do executável.";
            return;
        }

        LoadFolder(folder);
    }

    [RelayCommand(CanExecute = nameof(CanPrepare), IncludeCancelCommand = true)]
    private async Task PrepareAsync(CancellationToken cancellationToken)
    {
        if (ScanResult is not { CanPrepare: true } scan)
        {
            return;
        }

        Result = null;
        Log.Clear();
        ProgressPercent = 0;
        IsPreparing = true;
        SetSteps(StepState.Done, StepState.Done, StepState.Active, StepState.Pending);

        try
        {
            var result = await _preparation.PrepareAsync(scan, new Progress<PreparationProgress>(OnProgress), cancellationToken);
            Result = result;

            if (result.Succeeded)
            {
                SetSteps(StepState.Done, StepState.Done, StepState.Done, StepState.Done);
                StatusMessage = "Banco preparado e validado.";
                AddLog($"Banco preparado em {result.DatabasePath}");
            }
            else
            {
                SetSteps(StepState.Done, StepState.Done, StepState.Error, StepState.Pending);
                StatusMessage = "Não foi possível preparar o banco.";
                AddLog($"Falha: {result.Error}");
            }
        }
        catch (OperationCanceledException)
        {
            ProgressPercent = 0;
            SetSteps(StepState.Done, StepState.Done, StepState.Pending, StepState.Pending);
            StatusMessage = "Preparação cancelada. Os backups originais não foram alterados.";
            AddLog("Preparação cancelada pelo usuário.");
        }
        finally
        {
            IsPreparing = false;
        }
    }

    [RelayCommand]
    private void OpenWorkFolder()
    {
        if (Result is { } result && Directory.Exists(result.WorkFolder))
        {
            _shell.OpenFolder(result.WorkFolder);
        }
    }

    [RelayCommand]
    private void CopyDatabasePath()
    {
        if (Result?.DatabasePath is { } path)
        {
            _shell.CopyToClipboard(path);
            StatusMessage = "Caminho do banco copiado.";
        }
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme;
        _theme.Apply(IsDarkTheme);
    }

    private bool CanPrepare() => IsIdle && ScanResult is { CanPrepare: true };

    private void LoadFolder(string folder)
    {
        FolderPath = folder;
        Result = null;
        Log.Clear();
        ProgressPercent = 0;

        try
        {
            var scan = _scanner.Scan(folder);
            ScanResult = scan;
            Backups = BuildItems(scan);
            SetSteps(StepState.Done, scan.CanPrepare ? StepState.Done : StepState.Error, StepState.Pending, StepState.Pending);
            StatusMessage = scan.CanPrepare
                ? $"Pronto para preparar: 1 backup completo e {Plural(scan.Modifications.Count, "modificação", "modificações")}."
                : "A verificação encontrou problemas. Corrija a pasta e selecione-a novamente.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ScanResult = null;
            Backups = [];
            SetSteps(StepState.Done, StepState.Error, StepState.Pending, StepState.Pending);
            StatusMessage = $"Não foi possível ler a pasta: {ex.Message}";
        }
    }

    private static IReadOnlyList<BackupItemViewModel> BuildItems(BackupScanResult scan)
    {
        var items = scan.Sequence
            .Select((backup, index) => BackupItemViewModel.From(
                backup,
                (index + 1).ToString(Culture),
                backup.Kind == BackupKind.Full ? BackupTone.Full : BackupTone.Modification))
            .ToList();

        items.AddRange(scan.Ignored.Select(backup => BackupItemViewModel.From(
            backup,
            "—",
            BackupTone.Ignored,
            backup.Kind == BackupKind.Full ? "Completo mais antigo: não utilizado" : "Anterior ao completo: já incluída nele")));

        return items;
    }

    private void OnProgress(PreparationProgress update)
    {
        ProgressPercent = update.Percent;
        StatusMessage = update.Message;
        AddLog(update.Message);
    }

    private void AddLog(string message) => Log.Add(new LogLine(DateTime.Now.ToString("HH:mm:ss", Culture), message));

    private void SetSteps(params StepState[] states)
    {
        for (var i = 0; i < Steps.Count && i < states.Length; i++)
        {
            Steps[i].State = states[i];
        }
    }

    private static string Plural(int count, string singular, string plural) =>
        count == 1 ? $"1 {singular}" : $"{count} {plural}";
}
