using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using BuscaTabelas.App.Helpers;
using BuscaTabelas.App.Services;
using BuscaTabelas.Core.Configuration;
using BuscaTabelas.Core.Services;

namespace BuscaTabelas.App.ViewModels;

/// <param name="Group">Grupo da barra lateral: vazio (principal) ou "SISTEMA".</param>
public sealed record NavigationItem(string Title, string Glyph, Type PageType, string Group = "");

/// <summary>Estado do rodapé da barra lateral.</summary>
public enum SidebarStatus
{
    Checking,
    Ready,
    Missing,
    Busy
}

public sealed partial class MainViewModel : ObservableObject
{
    private const string SystemGroup = "SISTEMA";

    private readonly INavigationService _navigation;
    private readonly ICatalogSession _catalog;
    private readonly ICatalogOpener _opener;
    private readonly IConfigurationService _configuration;
    private readonly CollectorViewModel _collector;

    [ObservableProperty]
    private NavigationItem? _selectedNavigationItem;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SidebarStatus), nameof(StatusText))]
    private bool _isInitialized;

    public MainViewModel(
        INavigationService navigation,
        ICatalogSession catalog,
        ICatalogOpener opener,
        IConfigurationService configuration,
        CollectorViewModel collector)
    {
        _navigation = navigation;
        _catalog = catalog;
        _opener = opener;
        _configuration = configuration;
        _collector = collector;

        NavigationItems =
        [
            new("Início", Glyphs.Home, typeof(HomeViewModel)),
            new("Pesquisar", Glyphs.Search, typeof(SearchViewModel)),
            new("Atualizar catálogo", Glyphs.Collector, typeof(CollectorViewModel)),
            new("Configurações", Glyphs.Settings, typeof(SettingsViewModel), SystemGroup),
            new("Logs", Glyphs.Logs, typeof(LogsViewModel), SystemGroup),
            new("Sobre", Glyphs.About, typeof(AboutViewModel), SystemGroup)
        ];

        // Menu em dois grupos: principal e SISTEMA (cabeçalho do grupo na barra lateral).
        NavigationView = new ListCollectionView(NavigationItems.ToList());
        NavigationView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(NavigationItem.Group)));

        _navigation.CurrentPageChanged += OnCurrentPageChanged;
        _catalog.Changed += (_, _) => RefreshStatus();
        _collector.PropertyChanged += OnCollectorChanged;
    }

    public IReadOnlyList<NavigationItem> NavigationItems { get; }

    public ICollectionView NavigationView { get; }

    public PageViewModel? CurrentPage => _navigation.CurrentPage;

    public string AppName => AppInfo.Name;

    public SidebarStatus SidebarStatus =>
        _catalog.IsLoading || _collector.IsRunning ? SidebarStatus.Busy
        : !IsInitialized ? SidebarStatus.Checking
        : _catalog.Current is not null ? SidebarStatus.Ready
        : SidebarStatus.Missing;

    public string StatusText => SidebarStatus switch
    {
        SidebarStatus.Busy => _collector.IsRunning ? "Coletor em execução…" : "Carregando catálogo…",
        SidebarStatus.Ready => _opener.IsDemoCatalog(_catalog.Current!.FilePath)
            ? "Catálogo de demonstração"
            : $"Catálogo de {Formats.DateTime(_catalog.Current!.Metadata.GeneratedAt)}",
        SidebarStatus.Missing => "Nenhum catálogo carregado",
        _ => "Iniciando…"
    };

    /// <param name="startupFile">catalogo.json recebido por linha de comando (ex.: "Abrir com").</param>
    public async Task InitializeAsync(string? startupFile = null)
    {
        _navigation.NavigateTo<HomeViewModel>();

        try
        {
            if (startupFile is not null && File.Exists(startupFile))
            {
                await _opener.OpenAsync(startupFile);
            }
            else if (_configuration.Current.OpenLatestCatalogOnStartup)
            {
                await _opener.OpenLatestAsync(quiet: true);
            }
            else
            {
                await _opener.OpenDemoAsync();
            }
        }
        finally
        {
            IsInitialized = true;
        }
    }

    partial void OnSelectedNavigationItemChanged(NavigationItem? value)
    {
        if (value is not null)
        {
            _navigation.NavigateTo(value.PageType);
        }
    }

    private void OnCurrentPageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(CurrentPage));
        SelectedNavigationItem = NavigationItems.FirstOrDefault(i => i.PageType == _navigation.CurrentPage?.GetType());
    }

    private void OnCollectorChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CollectorViewModel.IsRunning))
        {
            RefreshStatus();
        }
    }

    private void RefreshStatus()
    {
        OnPropertyChanged(nameof(SidebarStatus));
        OnPropertyChanged(nameof(StatusText));
    }
}
