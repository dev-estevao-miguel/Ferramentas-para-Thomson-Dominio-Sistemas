using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BuscaTabelas.App.Helpers;
using BuscaTabelas.App.Services;
using BuscaTabelas.Core.Catalog;
using BuscaTabelas.Core.Services;

namespace BuscaTabelas.App.ViewModels;

/// <summary>Número em destaque na tela Início.</summary>
public sealed record CatalogMetric(string Label, string Value, string Detail);

/// <summary>Linha "rótulo: valor" do card do catálogo em uso.</summary>
public sealed record CatalogInfoRow(string Label, string Value, bool IsMono = false);

/// <summary>Início: pesquisa rápida, catálogo em uso e acesso ao coletor.</summary>
public sealed partial class HomeViewModel : PageViewModel
{
    private readonly ICatalogSession _session;
    private readonly ICatalogOpener _opener;
    private readonly INavigationService _navigation;
    private readonly IShellService _shell;
    private readonly SearchViewModel _search;

    [ObservableProperty]
    private string _quickSearch = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<CatalogMetric> _metrics = [];

    [ObservableProperty]
    private IReadOnlyList<CatalogInfoRow> _catalogInfo = [];

    [ObservableProperty]
    private IReadOnlyList<string> _warnings = [];

    public HomeViewModel(
        ICatalogSession session,
        ICatalogOpener opener,
        INavigationService navigation,
        IShellService shell,
        SearchViewModel search)
    {
        _session = session;
        _opener = opener;
        _navigation = navigation;
        _shell = shell;
        _search = search;
        _session.Changed += (_, _) => Refresh();
        Refresh();
    }

    public override string Title => "Início";

    public bool HasCatalog => _session.Current is not null;

    public bool IsLoading => _session.IsLoading;

    /// <summary>Nem catálogo nem carregamento: mostra o convite para exportar.</summary>
    public bool ShowEmptyState => !HasCatalog && !IsLoading;

    [RelayCommand]
    private void Search() => _search.SearchFor(QuickSearch.Trim());

    [RelayCommand]
    private void OpenCollector() => _navigation.NavigateTo<CollectorViewModel>();

    [RelayCommand]
    private Task OpenCatalogAsync() => _opener.BrowseAsync();

    [RelayCommand]
    private Task OpenDemoCatalogAsync() => _opener.OpenDemoAsync();

    [RelayCommand]
    private void RevealCatalog()
    {
        if (_session.Current is { } catalog)
        {
            _shell.RevealFile(catalog.FilePath);
        }
    }

    private void Refresh()
    {
        OnPropertyChanged(nameof(HasCatalog));
        OnPropertyChanged(nameof(IsLoading));
        OnPropertyChanged(nameof(ShowEmptyState));

        var catalog = _session.Current;
        if (catalog is null)
        {
            Metrics = [];
            CatalogInfo = [];
            Warnings = [];
            return;
        }

        var tables = catalog.CountOfType("BASE");
        var views = catalog.CountOfType("VIEW");
        var others = catalog.Tables.Count - tables - views;
        var describedTables = catalog.Tables.Count(t => t.HasDescription);
        var describedColumns = catalog.Tables.Sum(t => t.Columns.Count(c => c.HasDescription));
        var columnsWithCodes = catalog.Tables.Sum(t => t.Columns.Count(c => c.Codes.Count > 0));

        Metrics =
        [
            new("Tabelas", Formats.Number(tables), $"{Formats.Number(views)} views · {Formats.Number(others)} temporárias"),
            new("Colunas", Formats.Number(catalog.ColumnCount), $"{Percent(describedColumns, catalog.ColumnCount)} com descrição"),
            new("Códigos documentados", Formats.Number(catalog.CodeCount), $"em {Formats.Count(columnsWithCodes, "coluna", "colunas")}"),
            new("Objetos com descrição", Percent(describedTables, catalog.Tables.Count), $"{Formats.Number(describedTables)} de {Formats.Number(catalog.Tables.Count)}")
        ];

        var metadata = catalog.Metadata;
        CatalogInfo =
        [
            new("Extraído em", Formats.DateTime(metadata.GeneratedAt)),
            new("Banco", metadata.Server.Database ?? "não informado"),
            new("SQL Anywhere", metadata.Server.Version ?? "não informado"),
            new("Esquema", metadata.Schema ?? "não informado"),
            new("Dicionário de códigos", DescribeValuesStatus(metadata.ValuesStatus)),
            new("Arquivo", catalog.FilePath, IsMono: true)
        ];
        Warnings = metadata.Warnings;
    }

    private static string Percent(int part, int total) =>
        total == 0 ? "0%" : (part * 100.0 / total).ToString("0", Formats.Culture) + "%";

    private static string DescribeValuesStatus(string? status) => status switch
    {
        "disponivel" => "Disponível",
        "vazio" => "Consultado, sem códigos",
        "indisponivel" => "Indisponível na exportação",
        null => "não informado",
        _ => status
    };
}
