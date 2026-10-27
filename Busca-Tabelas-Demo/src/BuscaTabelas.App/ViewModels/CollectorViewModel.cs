using System.ComponentModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BuscaTabelas.App.Helpers;
using BuscaTabelas.App.Services;
using BuscaTabelas.Core.Catalog;
using BuscaTabelas.Core.Collector;
using BuscaTabelas.Core.Configuration;
using BuscaTabelas.Core.Logging;
using BuscaTabelas.Core.Services;

namespace BuscaTabelas.App.ViewModels;

public enum CollectorMessageKind
{
    None,
    Success,
    Warning
}

/// <summary>Exportação listada na tela, com indicação da que está em uso.</summary>
public sealed record CatalogFileItem(CatalogFileInfo File, bool IsCurrent)
{
    public string Title => Formats.DateTime(File.ExportedAt);

    public string Detail => $"{File.FolderName} · {Formats.FileSize(File.SizeBytes)}";
}

/// <summary>
/// Atualizar catálogo: roda o coletor (tools/coletor/BT.ps1) em uma janela do PowerShell de 32 bits.
/// A senha é digitada nessa janela e nunca passa pelo aplicativo.
/// </summary>
public sealed partial class CollectorViewModel : PageViewModel
{
    private readonly ICollectorService _collector;
    private readonly ICatalogOpener _opener;
    private readonly ICatalogSession _session;
    private readonly IConfigurationService _configuration;
    private readonly IDialogService _dialogs;
    private readonly IShellService _shell;
    private readonly ILoggingService _logger;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TestConnectionCommand), nameof(ExportCommand))]
    private string _dsn;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TestConnectionCommand), nameof(ExportCommand))]
    private string _user;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TestConnectionCommand), nameof(ExportCommand))]
    private string _schema;

    [ObservableProperty]
    private IReadOnlyList<OdbcDataSource> _dataSources = [];

    [ObservableProperty]
    private IReadOnlyList<CatalogFileItem> _catalogs = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyCanExecuteChangedFor(nameof(TestConnectionCommand), nameof(ExportCommand))]
    private bool _isRunning;

    [ObservableProperty]
    private string _runningText = string.Empty;

    [ObservableProperty]
    private CollectorMessageKind _messageKind;

    [ObservableProperty]
    private string _message = string.Empty;

    /// <summary>Exportação via ODBC liberada em Configurações (desabilitada por padrão na demonstração).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCollectorDisabled))]
    [NotifyCanExecuteChangedFor(nameof(TestConnectionCommand), nameof(ExportCommand))]
    private bool _isCollectorEnabled;

    public CollectorViewModel(
        ICollectorService collector,
        ICatalogOpener opener,
        ICatalogSession session,
        IConfigurationService configuration,
        IDialogService dialogs,
        IShellService shell,
        ILoggingService logger)
    {
        _collector = collector;
        _opener = opener;
        _session = session;
        _configuration = configuration;
        _dialogs = dialogs;
        _shell = shell;
        _logger = logger;

        var settings = configuration.Current;
        _dsn = settings.CollectorDsn;
        _user = settings.CollectorUser;
        _schema = settings.CollectorSchema;
        _isCollectorEnabled = settings.CollectorEnabled;

        _session.Changed += (_, _) => RefreshCatalogs();
        configuration.SettingsChanged += (_, changed) =>
        {
            // Valores padrão alterados em Configurações.
            if (!IsRunning)
            {
                Dsn = changed.CollectorDsn;
                User = changed.CollectorUser;
                Schema = changed.CollectorSchema;
            }

            IsCollectorEnabled = changed.CollectorEnabled;
            RefreshDataSources();
            RefreshCatalogs();
        };
    }

    public override string Title => "Atualizar catálogo";

    public bool IsIdle => !IsRunning;

    public bool IsCollectorDisabled => !IsCollectorEnabled;

    public string CatalogsFolder => _opener.CatalogsFolder;

    public bool HasCatalogs => Catalogs.Count > 0;

    public bool HasDataSources => DataSources.Count > 0;

    public override Task OnNavigatedToAsync()
    {
        RefreshDataSources();
        RefreshCatalogs();
        return Task.CompletedTask;
    }

    [RelayCommand]
    private void UseDataSource(OdbcDataSource source) => Dsn = source.Name;

    [RelayCommand]
    private Task OpenDemoCatalogAsync() => _opener.OpenDemoAsync();

    [RelayCommand(CanExecute = nameof(CanRun))]
    private Task TestConnectionAsync() => RunAsync(CollectorMode.Testar);

    [RelayCommand(CanExecute = nameof(CanRun))]
    private Task ExportAsync() => RunAsync(CollectorMode.Exportar);

    [RelayCommand]
    private async Task OpenCatalogAsync(CatalogFileItem item)
    {
        if (await _opener.OpenAsync(item.File.Path))
        {
            ShowMessage(CollectorMessageKind.Success, $"Catálogo de {item.Title} carregado.");
        }
    }

    [RelayCommand]
    private Task BrowseAsync() => _opener.BrowseAsync();

    [RelayCommand]
    private void OpenCatalogsFolder()
    {
        Directory.CreateDirectory(CatalogsFolder);
        _shell.OpenFolder(CatalogsFolder);
    }

    [RelayCommand]
    private void DismissMessage() => ShowMessage(CollectorMessageKind.None, string.Empty);

    private bool CanRun() =>
        IsCollectorEnabled && !IsRunning
        && !string.IsNullOrWhiteSpace(Dsn) && !string.IsNullOrWhiteSpace(User) && !string.IsNullOrWhiteSpace(Schema);

    /// <summary>Com o coletor desabilitado, o registro do Windows nem é lido.</summary>
    private void RefreshDataSources()
    {
        DataSources = IsCollectorEnabled ? OdbcDataSources.ListSqlAnywhere() : [];
        OnPropertyChanged(nameof(HasDataSources));
    }

    private async Task RunAsync(CollectorMode mode)
    {
        if (!IsCollectorEnabled)
        {
            return;
        }

        var request = new CollectorRequest(
            mode, Dsn.Trim(), User.Trim(), Schema.Trim(),
            _configuration.Current.CollectorTimeoutSeconds, CatalogsFolder);

        RememberConnection(request);
        ShowMessage(CollectorMessageKind.None, string.Empty);
        RunningText = mode == CollectorMode.Exportar
            ? "Exportando… informe a senha na janela do PowerShell e aguarde a conclusão."
            : "Testando a conexão… informe a senha na janela do PowerShell.";
        IsRunning = true;

        try
        {
            var result = await _collector.RunAsync(request);
            if (!result.Succeeded)
            {
                ShowMessage(CollectorMessageKind.Warning,
                    "O coletor não foi concluído. A mensagem do erro ficou na janela do PowerShell: confira conexão, usuário e senha.");
            }
            else if (mode == CollectorMode.Testar)
            {
                ShowMessage(CollectorMessageKind.Success, "Conexão testada. Confira na janela do PowerShell o banco e as tabelas visíveis.");
            }
            else if (result.NewCatalog is { } created)
            {
                RefreshCatalogs();
                if (await _opener.OpenAsync(created.Path))
                {
                    ShowMessage(CollectorMessageKind.Success, $"Catálogo exportado e carregado ({Formats.DateTime(created.ExportedAt)}).");
                }
            }
            else
            {
                ShowMessage(CollectorMessageKind.Warning, $"O coletor terminou, mas nenhum catálogo novo foi encontrado em {CatalogsFolder}.");
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            _logger.Error("Falha ao executar o coletor.", ex);
            _dialogs.ShowError("Não foi possível abrir o coletor",
                "O PowerShell de 32 bits não pôde ser iniciado para exportar o catálogo.", $"{_collector.PowerShellPath}\n{ex.Message}");
        }
        finally
        {
            IsRunning = false;
            RefreshCatalogs();
        }
    }

    /// <summary>A conexão usada fica como padrão para a próxima vez.</summary>
    private void RememberConnection(CollectorRequest request)
    {
        try
        {
            _configuration.Update(settings =>
            {
                settings.CollectorDsn = request.Dsn;
                settings.CollectorUser = request.User;
                settings.CollectorSchema = request.Schema;
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.Warning("Não foi possível salvar a conexão do coletor.", ex);
        }
    }

    private void RefreshCatalogs()
    {
        var current = _session.Current?.FilePath;
        Catalogs = CatalogLocator.List(CatalogsFolder)
            .Select(c => new CatalogFileItem(c, string.Equals(c.Path, current, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        OnPropertyChanged(nameof(HasCatalogs));
        OnPropertyChanged(nameof(CatalogsFolder));
    }

    private void ShowMessage(CollectorMessageKind kind, string message)
    {
        MessageKind = kind;
        Message = message;
    }
}
