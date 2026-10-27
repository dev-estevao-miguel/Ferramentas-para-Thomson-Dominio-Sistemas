using System.ComponentModel;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BuscaTabelas.App.Services;
using BuscaTabelas.Core.Configuration;
using BuscaTabelas.Core.Logging;

namespace BuscaTabelas.App.ViewModels;

/// <summary>
/// Edita uma cópia das configurações. Nada é gravado até o usuário clicar em "Salvar alterações".
/// O tema é aplicado na hora como pré-visualização e revertido se as alterações forem descartadas.
/// </summary>
public sealed partial class SettingsViewModel : PageViewModel
{
    private static readonly HashSet<string> NonSettingProperties = [nameof(HasChanges), nameof(StatusMessage)];

    private readonly IConfigurationService _configuration;
    private readonly IThemeService _themeService;
    private readonly IDialogService _dialogs;
    private readonly ILoggingService _logger;
    private bool _isLoading;

    // Catálogos
    [ObservableProperty]
    private string? _catalogsFolder;

    [ObservableProperty]
    private bool _openLatestCatalogOnStartup;

    // Coletor
    [ObservableProperty]
    private bool _collectorEnabled;

    [ObservableProperty]
    private string _collectorDsn = string.Empty;

    [ObservableProperty]
    private string _collectorUser = string.Empty;

    [ObservableProperty]
    private string _collectorSchema = string.Empty;

    [ObservableProperty]
    private string _collectorTimeout = string.Empty;

    // Pesquisa
    [ObservableProperty]
    private string _searchLimit = string.Empty;

    // Aparência
    [ObservableProperty]
    private AppTheme _theme;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(DiscardCommand))]
    private bool _hasChanges;

    [ObservableProperty]
    private string? _statusMessage;

    public SettingsViewModel(
        IConfigurationService configuration,
        IThemeService theme,
        IDialogService dialogs,
        ILoggingService logger)
    {
        _configuration = configuration;
        _themeService = theme;
        _dialogs = dialogs;
        _logger = logger;
        LoadFrom(configuration.Current);
    }

    public override string Title => "Configurações";

    public string CatalogsFolderPlaceholder => $"Padrão: {AppPaths.DefaultCatalogsFolder}";

    public override Task OnNavigatedToAsync()
    {
        if (!HasChanges)
        {
            LoadFrom(_configuration.Current);
        }

        StatusMessage = null;
        return Task.CompletedTask;
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (_isLoading || e.PropertyName is null || NonSettingProperties.Contains(e.PropertyName))
        {
            return;
        }

        HasChanges = TryBuildSettings(out var settings, out _) is false || !AreEqual(settings!, _configuration.Current);
        StatusMessage = null;
    }

    partial void OnThemeChanged(AppTheme value) => _themeService.Apply(value);

    [RelayCommand]
    private void BrowseCatalogsFolder() =>
        CatalogsFolder = _dialogs.SelectFolder("Pasta dos catálogos", CatalogsFolder ?? AppPaths.DefaultCatalogsFolder) ?? CatalogsFolder;

    [RelayCommand(CanExecute = nameof(HasChanges))]
    private void Save()
    {
        if (!TryBuildSettings(out var settings, out var error))
        {
            _dialogs.ShowError("Não foi possível salvar", error!);
            return;
        }

        try
        {
            _configuration.Save(settings!);
            HasChanges = false;
            StatusMessage = "Configurações salvas.";
            _logger.Information("Configurações atualizadas pelo usuário.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.Error("Falha ao salvar as configurações.", ex);
            _dialogs.ShowError("Não foi possível salvar", "As configurações não puderam ser gravadas.", ex.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(HasChanges))]
    private void Discard()
    {
        LoadFrom(_configuration.Current);
        _themeService.Apply(_configuration.Current.Theme);
        StatusMessage = "Alterações descartadas.";
    }

    private void LoadFrom(AppSettings settings)
    {
        _isLoading = true;
        try
        {
            CatalogsFolder = settings.CatalogsFolder;
            OpenLatestCatalogOnStartup = settings.OpenLatestCatalogOnStartup;
            CollectorEnabled = settings.CollectorEnabled;
            CollectorDsn = settings.CollectorDsn;
            CollectorUser = settings.CollectorUser;
            CollectorSchema = settings.CollectorSchema;
            CollectorTimeout = settings.CollectorTimeoutSeconds.ToString(CultureInfo.InvariantCulture);
            SearchLimit = settings.SearchLimit.ToString(CultureInfo.InvariantCulture);
            Theme = settings.Theme;
        }
        finally
        {
            _isLoading = false;
        }

        HasChanges = false;
    }

    /// <returns>Falso com a mensagem de erro quando algum campo é inválido.</returns>
    private bool TryBuildSettings(out AppSettings? settings, out string? error)
    {
        settings = null;
        error = null;

        var folder = Clean(CatalogsFolder);
        if (folder is not null && !Path.IsPathFullyQualified(folder))
        {
            error = "Pasta dos catálogos: informe um caminho completo (ex.: C:\\Catalogos).";
            return false;
        }

        if (!int.TryParse(CollectorTimeout, NumberStyles.Integer, CultureInfo.InvariantCulture, out var timeout) || timeout is < 1 or > 300)
        {
            error = "Tempo limite do coletor: informe um número de 1 a 300 segundos.";
            return false;
        }

        if (!int.TryParse(SearchLimit, NumberStyles.Integer, CultureInfo.InvariantCulture, out var limit) || limit is < 50 or > 5000)
        {
            error = "Limite de resultados: informe um número de 50 a 5000.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(CollectorSchema))
        {
            error = "Esquema: informe o esquema do banco (ex.: DBA).";
            return false;
        }

        settings = _configuration.Current.Clone();
        settings.CatalogsFolder = folder;
        settings.OpenLatestCatalogOnStartup = OpenLatestCatalogOnStartup;
        settings.CollectorEnabled = CollectorEnabled;
        settings.CollectorDsn = CollectorDsn.Trim();
        settings.CollectorUser = CollectorUser.Trim();
        settings.CollectorSchema = CollectorSchema.Trim();
        settings.CollectorTimeoutSeconds = timeout;
        settings.SearchLimit = limit;
        settings.Theme = Theme;
        return true;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool AreEqual(AppSettings a, AppSettings b) =>
        a.CatalogsFolder == b.CatalogsFolder
        && a.OpenLatestCatalogOnStartup == b.OpenLatestCatalogOnStartup
        && a.CollectorEnabled == b.CollectorEnabled
        && a.CollectorDsn == b.CollectorDsn
        && a.CollectorUser == b.CollectorUser
        && a.CollectorSchema == b.CollectorSchema
        && a.CollectorTimeoutSeconds == b.CollectorTimeoutSeconds
        && a.SearchLimit == b.SearchLimit
        && a.Theme == b.Theme;
}
