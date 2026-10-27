namespace BuscaTabelas.Core.Configuration;

public enum AppTheme
{
    Light,
    Dark,
    System
}

/// <summary>
/// Configurações persistidas do aplicativo. Caminhos vazios significam "usar o padrão".
/// </summary>
public sealed class AppSettings
{
    public const int DefaultSearchLimit = 300;
    public const int DefaultCollectorTimeoutSeconds = 120;

    // Catálogo
    /// <summary>Pasta das exportações. Vazio = <see cref="AppPaths.DefaultCatalogsFolder"/>.</summary>
    public string? CatalogsFolder { get; set; }

    /// <summary>Último catálogo aberto manualmente (fora da pasta de exportações).</summary>
    public string? LastCatalogPath { get; set; }

    /// <summary>Ao abrir, carrega a exportação mais recente da pasta (ou o último arquivo aberto).</summary>
    public bool OpenLatestCatalogOnStartup { get; set; } = true;

    // Coletor ODBC
    /// <summary>
    /// Exportação via ODBC. Desabilitada por padrão: a demonstração funciona só com o catálogo fictício,
    /// sem ler conexões do Windows nem conectar a nenhum banco.
    /// </summary>
    public bool CollectorEnabled { get; set; }

    public string CollectorDsn { get; set; } = "DSN_EXEMPLO";
    public string CollectorUser { get; set; } = "USUARIO_EXEMPLO";
    public string CollectorSchema { get; set; } = "DBA";
    public int CollectorTimeoutSeconds { get; set; } = DefaultCollectorTimeoutSeconds;

    // Pesquisa
    /// <summary>Quantidade máxima de resultados exibidos; o total encontrado continua sendo informado.</summary>
    public int SearchLimit { get; set; } = DefaultSearchLimit;

    // Aparência
    public AppTheme Theme { get; set; } = AppTheme.Light;

    // Janela (restaurada na próxima abertura, sempre limitada ao tamanho da tela)
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public bool WindowMaximized { get; set; }

    public AppSettings Clone() => (AppSettings)MemberwiseClone();
}
