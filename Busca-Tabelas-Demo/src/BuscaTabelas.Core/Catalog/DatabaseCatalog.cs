namespace BuscaTabelas.Core.Catalog;

/// <summary>
/// Catálogo exportado pelo coletor (formato <see cref="CatalogLoader.Format"/>), já validado e indexado.
/// É somente leitura e pode ser pesquisado de qualquer thread.
/// </summary>
public sealed class DatabaseCatalog
{
    private readonly Dictionary<int, CatalogTable> _tablesById;
    private readonly CatalogIndex _index;

    internal DatabaseCatalog(
        string filePath,
        CatalogMetadata metadata,
        IReadOnlyList<CatalogTable> tables,
        int columnCount,
        int codeCount)
    {
        FilePath = filePath;
        Metadata = metadata;
        Tables = tables;
        ColumnCount = columnCount;
        CodeCount = codeCount;
        _tablesById = tables.ToDictionary(t => t.Id);
        _index = new CatalogIndex(tables);
    }

    public string FilePath { get; }

    public CatalogMetadata Metadata { get; }

    /// <summary>Tabelas, views e temporárias, na ordem do arquivo (por nome).</summary>
    public IReadOnlyList<CatalogTable> Tables { get; }

    public int ColumnCount { get; }

    public int CodeCount { get; }

    public int CountOfType(string type) => Tables.Count(t => string.Equals(t.Type, type, StringComparison.OrdinalIgnoreCase));

    public CatalogTable? FindTable(int tableId) => _tablesById.GetValueOrDefault(tableId);

    public CatalogSearchResult Search(CatalogQuery query) => _index.Search(query);
}

/// <summary>Informações da extração gravadas pelo coletor.</summary>
public sealed record CatalogMetadata(
    string Format,
    DateTimeOffset? GeneratedAt,
    string? GeneratedAtText,
    CatalogServerInfo Server,
    string? Schema,
    string? Scope,
    string? ValuesStatus,
    IReadOnlyList<string> Warnings);
