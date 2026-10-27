using System.Diagnostics;
using BuscaTabelas.Core.Catalog;
using BuscaTabelas.Core.Logging;

namespace BuscaTabelas.Core.Services;

/// <summary>Catálogo em uso pelo aplicativo, compartilhado por todas as telas.</summary>
public interface ICatalogSession
{
    DatabaseCatalog? Current { get; }

    bool IsLoading { get; }

    /// <summary>Catálogo carregado, trocado ou carregamento iniciado/terminado.</summary>
    event EventHandler? Changed;

    /// <summary>Carrega em segundo plano. Em caso de erro, o catálogo anterior continua em uso e a exceção é repassada.</summary>
    /// <exception cref="CatalogFormatException" />
    /// <exception cref="IOException" />
    Task<DatabaseCatalog> LoadAsync(string path);
}

public sealed class CatalogSession : ICatalogSession
{
    private readonly ICatalogLoader _loader;
    private readonly ILoggingService _logger;
    private int _loadVersion;

    public CatalogSession(ICatalogLoader loader, ILoggingService logger)
    {
        _loader = loader;
        _logger = logger;
    }

    public DatabaseCatalog? Current { get; private set; }

    public bool IsLoading { get; private set; }

    public event EventHandler? Changed;

    public async Task<DatabaseCatalog> LoadAsync(string path)
    {
        // Só o último pedido de carga define o catálogo em uso.
        var version = Interlocked.Increment(ref _loadVersion);
        SetLoading(true);
        _logger.Information($"Carregando catálogo: {path}");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var catalog = await Task.Run(() => _loader.Load(path));
            _logger.Information(
                $"Catálogo carregado em {stopwatch.Elapsed.TotalSeconds:0.0} s: {catalog.Tables.Count} tabelas/views, " +
                $"{catalog.ColumnCount} colunas, {catalog.CodeCount} códigos.");

            if (version == _loadVersion)
            {
                Current = catalog;
            }

            return catalog;
        }
        catch (Exception ex) when (ex is CatalogFormatException or IOException or UnauthorizedAccessException)
        {
            _logger.Warning($"Não foi possível carregar o catálogo '{path}'.", ex);
            throw;
        }
        finally
        {
            if (version == _loadVersion)
            {
                SetLoading(false);
            }
        }
    }

    private void SetLoading(bool value)
    {
        IsLoading = value;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
