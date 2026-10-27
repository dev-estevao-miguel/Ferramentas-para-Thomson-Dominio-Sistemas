namespace BuscaTabelas.Core.Configuration;

/// <summary>
/// Locais de dados do aplicativo. Ficam em %LocalAppData% para funcionar mesmo quando
/// o executável estiver em uma pasta sem permissão de escrita (Program Files ou rede).
/// A demonstração usa uma pasta própria, separada de qualquer outra instalação.
/// </summary>
public static class AppPaths
{
    public static string DataFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BuscaTabelasDemo");

    /// <summary>Catálogo fictício distribuído ao lado do executável (samples/catalogo-demo.json).</summary>
    public static string DemoCatalogFile => Path.Combine(AppContext.BaseDirectory, "samples", "catalogo-demo.json");

    public static string SettingsFile => Path.Combine(DataFolder, "settings.json");

    public static string LogsFolder => Path.Combine(DataFolder, "Logs");

    /// <summary>Destino padrão das exportações do coletor: uma subpasta por exportação, com o catalogo.json.</summary>
    public static string DefaultCatalogsFolder => Path.Combine(DataFolder, "Catalogos");

    /// <summary>Cópia do coletor (BT.ps1 + sql) extraída do executável a cada uso.</summary>
    public static string CollectorFolder => Path.Combine(DataFolder, "Coletor");

    public static string ResolveCatalogsFolder(AppSettings settings) =>
        string.IsNullOrWhiteSpace(settings.CatalogsFolder) ? DefaultCatalogsFolder : settings.CatalogsFolder;
}
