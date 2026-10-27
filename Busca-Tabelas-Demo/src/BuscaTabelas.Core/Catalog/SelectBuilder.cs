namespace BuscaTabelas.Core.Catalog;

/// <summary>Monta o texto de um SELECT com as colunas escolhidas. O BT não executa consultas.</summary>
public static class SelectBuilder
{
    /// <exception cref="ArgumentException">Nenhuma coluna, ou coluna de outra tabela.</exception>
    public static string Build(CatalogTable table, IEnumerable<CatalogColumn> columns)
    {
        var selected = columns.Distinct().ToList();
        if (selected.Count == 0 || selected.Any(c => !ReferenceEquals(c.Table, table)))
        {
            throw new ArgumentException("Selecione uma ou mais colunas desta tabela.", nameof(columns));
        }

        var lines = selected.OrderBy(c => c.Id).Select(c => "    " + QuoteIdentifier(c.Name));
        return "SELECT\n" + string.Join(",\n", lines) + "\nFROM " + QuoteIdentifier(table.Schema) + "." + QuoteIdentifier(table.Name) + ";";
    }

    /// <summary>Identificador entre aspas duplas, com aspas internas duplicadas.</summary>
    public static string QuoteIdentifier(string name) => "\"" + name.Replace("\"", "\"\"") + "\"";
}
