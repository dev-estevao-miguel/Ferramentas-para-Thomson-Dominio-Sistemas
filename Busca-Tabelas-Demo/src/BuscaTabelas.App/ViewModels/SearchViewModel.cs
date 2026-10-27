using System.ComponentModel;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BuscaTabelas.App.Helpers;
using BuscaTabelas.App.Services;
using BuscaTabelas.Core.Catalog;
using BuscaTabelas.Core.Configuration;
using BuscaTabelas.Core.Logging;
using BuscaTabelas.Core.Services;

namespace BuscaTabelas.App.ViewModels;

/// <summary>Painel inferior da tabela aberta.</summary>
public enum DetailTab
{
    Column,
    Select
}

/// <summary>
/// Pesquisa offline no catálogo carregado. Nada aqui conecta ao banco: o SELECT é só texto e o
/// contexto para IA é salvo em arquivo local.
/// </summary>
public sealed partial class SearchViewModel : PageViewModel
{
    private static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(200);

    private readonly ICatalogSession _session;
    private readonly ICatalogOpener _opener;
    private readonly IConfigurationService _configuration;
    private readonly IDialogService _dialogs;
    private readonly IShellService _shell;
    private readonly INavigationService _navigation;
    private readonly ILoggingService _logger;

    private DatabaseCatalog? _catalog;
    private CancellationTokenSource? _searchCancellation;
    private CancellationTokenSource? _statusCancellation;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private CatalogScope _scope = CatalogScope.All;

    [ObservableProperty]
    private CatalogSearchField _field = CatalogSearchField.NameAndDescription;

    [ObservableProperty]
    private IReadOnlyList<SearchHitItem> _results = [];

    [ObservableProperty]
    private string _resultSummary = string.Empty;

    [ObservableProperty]
    private bool _isResultTruncated;

    [ObservableProperty]
    private SearchHitItem? _selectedResult;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTable), nameof(TableTitle), nameof(TableTypeText), nameof(TableDescription))]
    [NotifyCanExecuteChangedFor(nameof(ExportContextCommand), nameof(CopyTableNameCommand), nameof(SelectAllColumnsCommand))]
    private CatalogTable? _table;

    [ObservableProperty]
    private IReadOnlyList<ColumnItem> _columns = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDetailColumn))]
    [NotifyCanExecuteChangedFor(nameof(CopyColumnNameCommand))]
    private ColumnItem? _detailColumn;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectSql))]
    [NotifyCanExecuteChangedFor(nameof(CopySelectCommand))]
    private string _selectSql = string.Empty;

    [ObservableProperty]
    private string _selectionText = string.Empty;

    [ObservableProperty]
    private DetailTab _detailTab = DetailTab.Column;

    [ObservableProperty]
    private string? _statusMessage;

    public SearchViewModel(
        ICatalogSession session,
        ICatalogOpener opener,
        IConfigurationService configuration,
        IDialogService dialogs,
        IShellService shell,
        INavigationService navigation,
        ILoggingService logger)
    {
        _session = session;
        _opener = opener;
        _configuration = configuration;
        _dialogs = dialogs;
        _shell = shell;
        _navigation = navigation;
        _logger = logger;
        _session.Changed += (_, _) => OnCatalogChanged();
    }

    public override string Title => "Pesquisar";

    public bool HasCatalog => _session.Current is not null;

    public bool IsLoadingCatalog => _session.IsLoading;

    public bool HasTable => Table is not null;

    public bool HasDetailColumn => DetailColumn is not null;

    public bool HasSelectSql => SelectSql.Length > 0;

    public string TableTitle => Table?.FullName ?? string.Empty;

    public string TableTypeText => Table?.TypeDisplay ?? string.Empty;

    public string TableDescription => Table is null ? string.Empty : Table.HasDescription ? Table.Description : "Sem descrição no catálogo.";

    public override Task OnNavigatedToAsync()
    {
        if (!ReferenceEquals(_catalog, _session.Current))
        {
            OnCatalogChanged();
        }

        return Task.CompletedTask;
    }

    /// <summary>Abre a página já pesquisando (campo de busca da tela Início).</summary>
    public void SearchFor(string text)
    {
        _navigation.NavigateTo<SearchViewModel>();
        if (SearchText == text)
        {
            RunSearch(debounce: false);
        }
        else
        {
            SearchText = text;
        }
    }

    partial void OnSearchTextChanged(string value) => RunSearch(debounce: true);

    partial void OnScopeChanged(CatalogScope value) => RunSearch(debounce: false);

    partial void OnFieldChanged(CatalogSearchField value) => RunSearch(debounce: false);

    partial void OnSelectedResultChanged(SearchHitItem? value)
    {
        if (value is not null)
        {
            ShowTable(value.Hit.Table, value.Hit.Column);
        }
    }

    [RelayCommand]
    private void ClearSearch() => SearchText = string.Empty;

    [RelayCommand]
    private Task OpenCatalogAsync() => _opener.BrowseAsync();

    [RelayCommand]
    private void OpenCollector() => _navigation.NavigateTo<CollectorViewModel>();

    [RelayCommand(CanExecute = nameof(HasTable))]
    private void SelectAllColumns()
    {
        foreach (var column in Columns)
        {
            column.IsSelected = true;
        }
    }

    [RelayCommand]
    private void ClearColumnSelection()
    {
        foreach (var column in Columns)
        {
            column.IsSelected = false;
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectSql))]
    private void CopySelect()
    {
        if (_shell.CopyToClipboard(SelectSql))
        {
            ShowStatus("SELECT copiado. Valide os filtros e as regras do relatório antes de executá-lo.");
        }
    }

    [RelayCommand(CanExecute = nameof(HasTable))]
    private void CopyTableName()
    {
        if (Table is not null && _shell.CopyToClipboard(Table.FullName))
        {
            ShowStatus($"Nome copiado: {Table.FullName}");
        }
    }

    [RelayCommand(CanExecute = nameof(HasDetailColumn))]
    private void CopyColumnName()
    {
        if (DetailColumn is not null && _shell.CopyToClipboard(DetailColumn.Name))
        {
            ShowStatus($"Nome copiado: {DetailColumn.Name}");
        }
    }

    [RelayCommand(CanExecute = nameof(HasTable))]
    private void ExportContext()
    {
        if (_catalog is null || Table is null)
        {
            return;
        }

        var path = _dialogs.SelectSaveFile(
            "Exportar contexto da tabela para IA", "Markdown (*.md)|*.md", AiContextBuilder.SuggestFileName(Table), ".md");
        if (path is null)
        {
            return;
        }

        try
        {
            File.WriteAllText(path, AiContextBuilder.Build(_catalog, Table), new UTF8Encoding(false));
            _logger.Information($"Contexto da tabela {Table.FullName} exportado para {path}.");
            ShowStatus("Contexto salvo. Nenhum conteúdo foi enviado à internet.");
            _shell.RevealFile(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.Warning("Falha ao exportar o contexto da tabela.", ex);
            _dialogs.ShowError("Não foi possível salvar", "O arquivo de contexto não pôde ser gravado.", ex.Message);
        }
    }

    private void OnCatalogChanged()
    {
        OnPropertyChanged(nameof(HasCatalog));
        OnPropertyChanged(nameof(IsLoadingCatalog));
        if (ReferenceEquals(_catalog, _session.Current))
        {
            return;
        }

        _catalog = _session.Current;
        SelectedResult = null;
        ShowTable(null, null);
        RunSearch(debounce: false);
    }

    private async void RunSearch(bool debounce)
    {
        _searchCancellation?.Cancel();
        var cancellation = _searchCancellation = new CancellationTokenSource();
        var catalog = _catalog;
        if (catalog is null)
        {
            Results = [];
            ResultSummary = string.Empty;
            IsResultTruncated = false;
            return;
        }

        try
        {
            if (debounce)
            {
                await Task.Delay(SearchDelay, cancellation.Token);
            }

            var limit = Math.Clamp(_configuration.Current.SearchLimit, 50, 5000);
            var query = new CatalogQuery(SearchText, Scope, Field, limit);
            var result = await Task.Run(() => catalog.Search(query), cancellation.Token);
            if (cancellation.IsCancellationRequested)
            {
                return;
            }

            Results = result.Hits.Select(h => new SearchHitItem(h)).ToList();
            IsResultTruncated = result.IsTruncated;
            ResultSummary = result.Total switch
            {
                0 => "Nenhum resultado",
                _ when result.IsTruncated => $"{Formats.Number(result.Total)} resultados · exibindo {Formats.Number(result.Hits.Count)}",
                1 => "1 resultado",
                _ => $"{Formats.Number(result.Total)} resultados"
            };
        }
        catch (OperationCanceledException)
        {
            // Outra pesquisa começou antes desta terminar.
        }
    }

    private void ShowTable(CatalogTable? table, CatalogColumn? focus)
    {
        if (!ReferenceEquals(table, Table))
        {
            foreach (var column in Columns)
            {
                column.PropertyChanged -= OnColumnPropertyChanged;
            }

            Table = table;
            Columns = table?.Columns.Select(c => new ColumnItem(c)).ToList() ?? [];
            foreach (var column in Columns)
            {
                column.PropertyChanged += OnColumnPropertyChanged;
            }
        }

        if (focus is not null)
        {
            // Resultado do tipo coluna: só ela fica selecionada.
            foreach (var column in Columns)
            {
                column.IsSelected = ReferenceEquals(column.Column, focus);
            }

            DetailTab = DetailTab.Column;
        }

        UpdateSelection();
    }

    private void OnColumnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ColumnItem.IsSelected) || sender is not ColumnItem item)
        {
            return;
        }

        // O detalhe mostra a última coluna marcada; ao desmarcá-la, volta para outra ainda selecionada.
        if (item.IsSelected)
        {
            DetailColumn = item;
        }

        UpdateSelection();
    }

    private void UpdateSelection()
    {
        var selected = Columns.Where(c => c.IsSelected).ToList();
        if (DetailColumn is null || !DetailColumn.IsSelected || !Columns.Contains(DetailColumn))
        {
            DetailColumn = selected.FirstOrDefault();
        }

        SelectSql = Table is not null && selected.Count > 0 ? SelectBuilder.Build(Table, selected.Select(c => c.Column)) : string.Empty;
        var total = Table is null ? string.Empty : Formats.Count(Table.Columns.Count, "coluna", "colunas");
        SelectionText = selected.Count switch
        {
            0 => $"{total} · nenhuma selecionada",
            1 => $"{total} · 1 selecionada",
            _ => $"{total} · {selected.Count} selecionadas"
        };
    }

    private async void ShowStatus(string message)
    {
        _statusCancellation?.Cancel();
        var cancellation = _statusCancellation = new CancellationTokenSource();
        StatusMessage = message;
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(6), cancellation.Token);
            StatusMessage = null;
        }
        catch (TaskCanceledException)
        {
            // Outra mensagem substituiu esta.
        }
    }
}
