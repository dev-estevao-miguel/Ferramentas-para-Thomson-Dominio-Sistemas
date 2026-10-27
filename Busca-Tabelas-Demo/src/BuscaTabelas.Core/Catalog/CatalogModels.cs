namespace BuscaTabelas.Core.Catalog;

/// <summary>Banco e versão do SQL Anywhere de onde o catálogo foi exportado.</summary>
public sealed record CatalogServerInfo(string? Database, string? Version);

/// <summary>Tabela, view ou tabela temporária global (SYS.SYSTABLE).</summary>
public sealed class CatalogTable
{
    public required int Id { get; init; }
    public required string Schema { get; init; }
    public required string Name { get; init; }

    /// <summary>Tipo como veio do banco: BASE, VIEW, GBL TEMP...</summary>
    public required string Type { get; init; }

    public string Description { get; init; } = string.Empty;

    /// <summary>Colunas na ordem do banco (coluna_id).</summary>
    public IReadOnlyList<CatalogColumn> Columns { get; internal set; } = [];

    public string FullName => $"{Schema}.{Name}";

    public string TypeDisplay => CatalogTableTypes.Describe(Type);

    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
}

/// <summary>Coluna de uma tabela (SYS.SYSCOLUMN), com os códigos documentados no dicionário de dados.</summary>
public sealed class CatalogColumn
{
    public required int TableId { get; init; }
    public required int Id { get; init; }
    public required string Schema { get; init; }
    public required string TableName { get; init; }
    public required string Name { get; init; }
    public string Type { get; init; } = string.Empty;

    /// <summary>Tamanho ou precisão do tipo (não significa a mesma coisa para todos os tipos).</summary>
    public long? Size { get; init; }

    public long? Scale { get; init; }
    public bool IsPrimaryKey { get; init; }
    public bool? AllowsNull { get; init; }

    /// <summary>Valor padrão copiado do banco; nulo quando não há.</summary>
    public string? DefaultValue { get; init; }

    public string Description { get; init; } = string.Empty;

    public CatalogTable Table { get; internal set; } = null!;

    public IReadOnlyList<CatalogCode> Codes { get; internal set; } = [];

    public string FullName => $"{Schema}.{TableName}.{Name}";

    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
}

/// <summary>Código documentado de uma coluna no dicionário de dados, ex.: 1 = Ativo.</summary>
public sealed record CatalogCode(string Value, string Description);

public static class CatalogTableTypes
{
    public static string Describe(string type) => type.Trim().ToUpperInvariant() switch
    {
        "BASE" => "Tabela",
        "VIEW" => "View",
        "MAT VIEW" => "View materializada",
        "GBL TEMP" => "Temporária global",
        "TEXT" => "Configuração de texto",
        _ => type
    };
}
