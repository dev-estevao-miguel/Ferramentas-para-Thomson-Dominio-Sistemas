using System.IO;
using BuscaTabelas.Core.Catalog;
using BuscaTabelas.Core.Configuration;
using BuscaTabelas.Core.Logging;
using BuscaTabelas.Core.Services;

namespace BuscaTabelas.App.Services;

/// <summary>Abre catálogos a partir das telas: escolhe o arquivo, carrega e explica falhas ao usuário.</summary>
public interface ICatalogOpener
{
    string CatalogsFolder { get; }

    /// <returns>Falso se o arquivo não pôde ser carregado (o usuário já foi avisado).</returns>
    Task<bool> OpenAsync(string path);

    /// <summary>
    /// Exportação mais recente da pasta de catálogos ou, sem nenhuma, o último arquivo aberto.
    /// Sem nenhum dos dois, abre o catálogo fictício de demonstração.
    /// </summary>
    /// <param name="quiet">Sem diálogo quando não houver catálogo (uso na abertura do aplicativo).</param>
    Task<bool> OpenLatestAsync(bool quiet);

    /// <summary>Catálogo fictício (samples/catalogo-demo.json): funciona sem banco e sem conexão.</summary>
    Task<bool> OpenDemoAsync();

    /// <summary>Verdadeiro quando o caminho é o catálogo fictício de demonstração.</summary>
    bool IsDemoCatalog(string? path);

    Task<bool> BrowseAsync();
}

public sealed class CatalogOpener : ICatalogOpener
{
    private readonly ICatalogSession _session;
    private readonly IConfigurationService _configuration;
    private readonly IDialogService _dialogs;
    private readonly ILoggingService _logger;

    public CatalogOpener(ICatalogSession session, IConfigurationService configuration, IDialogService dialogs, ILoggingService logger)
    {
        _session = session;
        _configuration = configuration;
        _dialogs = dialogs;
        _logger = logger;
    }

    public string CatalogsFolder => AppPaths.ResolveCatalogsFolder(_configuration.Current);

    public async Task<bool> OpenAsync(string path)
    {
        try
        {
            await _session.LoadAsync(path);
        }
        catch (CatalogFormatException ex)
        {
            _dialogs.ShowError("Catálogo inválido", "O arquivo não pôde ser usado como catálogo do B.T.", ex.Message);
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _dialogs.ShowError("Não foi possível abrir o catálogo", "O arquivo não pôde ser lido.", ex.Message);
            return false;
        }

        try
        {
            _configuration.Update(settings => settings.LastCatalogPath = path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.Warning("Não foi possível lembrar o último catálogo aberto.", ex);
        }

        return true;
    }

    public async Task<bool> OpenLatestAsync(bool quiet)
    {
        var path = CatalogLocator.FindLatest(CatalogsFolder)?.Path;
        if (path is null && _configuration.Current.LastCatalogPath is { } last && File.Exists(last))
        {
            path = last;
        }

        if (path is null && File.Exists(AppPaths.DemoCatalogFile))
        {
            path = AppPaths.DemoCatalogFile;
        }

        if (path is null)
        {
            if (!quiet)
            {
                _dialogs.ShowInfo("Nenhum catálogo encontrado",
                    $"Não há exportações em {CatalogsFolder}.\n\nUse \"Abrir arquivo…\" para escolher um catalogo.json.");
            }

            return false;
        }

        return await OpenAsync(path);
    }

    public async Task<bool> OpenDemoAsync()
    {
        if (!File.Exists(AppPaths.DemoCatalogFile))
        {
            _dialogs.ShowError("Catálogo de demonstração ausente",
                "O arquivo samples/catalogo-demo.json não foi encontrado ao lado do executável.", AppPaths.DemoCatalogFile);
            return false;
        }

        return await OpenAsync(AppPaths.DemoCatalogFile);
    }

    public bool IsDemoCatalog(string? path) =>
        path is not null && string.Equals(Path.GetFullPath(path), Path.GetFullPath(AppPaths.DemoCatalogFile), StringComparison.OrdinalIgnoreCase);

    public async Task<bool> BrowseAsync()
    {
        var initial = Directory.Exists(CatalogsFolder) ? CatalogsFolder : null;
        var path = _dialogs.SelectFile("Abrir catálogo do B.T", "Catálogo do B.T (*.json)|*.json", initial);
        return path is not null && await OpenAsync(path);
    }
}
