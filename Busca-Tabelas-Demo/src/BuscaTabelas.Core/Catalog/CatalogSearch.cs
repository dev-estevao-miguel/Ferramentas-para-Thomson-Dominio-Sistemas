using System.Globalization;
using System.Text;

namespace BuscaTabelas.Core.Catalog;

public enum CatalogScope
{
    All,
    Tables,
    Columns
}

public enum CatalogSearchField
{
    NameAndDescription,
    Name,
    Description
}

/// <param name="Text">Palavras a procurar; todas precisam aparecer (sem diferenciar maiúsculas nem acentos).</param>
/// <param name="Limit">Quantidade máxima de resultados retornados; <see cref="CatalogSearchResult.Total"/> conta todos.</param>
public sealed record CatalogQuery(
    string? Text,
    CatalogScope Scope = CatalogScope.All,
    CatalogSearchField Field = CatalogSearchField.NameAndDescription,
    int Limit = 300);

/// <summary>Resultado da pesquisa: uma tabela, ou uma coluna (com a tabela dela).</summary>
public sealed record CatalogSearchHit(CatalogTable Table, CatalogColumn? Column)
{
    public bool IsColumn => Column is not null;

    public string FullName => Column?.FullName ?? Table.FullName;

    public string Description => Column?.Description ?? Table.Description;
}

/// <param name="Total">Total de itens encontrados, mesmo os que passaram do limite.</param>
public sealed record CatalogSearchResult(IReadOnlyList<CatalogSearchHit> Hits, int Total)
{
    public static CatalogSearchResult Empty { get; } = new([], 0);

    public bool IsTruncated => Total > Hits.Count;
}

public static class TextNormalizer
{
    /// <summary>Minúsculas e sem acentos: "Situação" → "situacao".</summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(c));
            }
        }

        return builder.ToString();
    }

    public static string[] Tokenize(string? value) =>
        Normalize(value).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
}

/// <summary>
/// Índice de pesquisa: nomes e descrições normalizados uma única vez ao carregar o catálogo.
/// Sem texto, devolve tudo na ordem do arquivo (tabelas e depois colunas). Com texto, os resultados
/// mais prováveis vêm primeiro: nome exato, nome que começa com o termo, termo no nome e, por último,
/// só na descrição.
/// </summary>
internal sealed class CatalogIndex
{
    private readonly Entry[] _entries;

    public CatalogIndex(IReadOnlyList<CatalogTable> tables)
    {
        var entries = new List<Entry>(tables.Count * 13);
        entries.AddRange(tables.Select(t => new Entry(new CatalogSearchHit(t, null), t.Name, t.FullName, t.Description)));
        entries.AddRange(tables.SelectMany(t => t.Columns)
            .Select(c => new Entry(new CatalogSearchHit(c.Table, c), c.Name, c.FullName, c.Description)));
        _entries = entries.ToArray();
    }

    public CatalogSearchResult Search(CatalogQuery query)
    {
        var limit = Math.Max(0, query.Limit);
        var tokens = TextNormalizer.Tokenize(query.Text);

        var matches = new List<(Entry Entry, int Rank)>();
        foreach (var entry in _entries)
        {
            if (!InScope(entry, query.Scope) || !Matches(entry, tokens, query.Field))
            {
                continue;
            }

            matches.Add((entry, tokens.Length == 0 ? 0 : Rank(entry, tokens)));
        }

        // OrderBy é estável: no mesmo nível, mantém a ordem do arquivo.
        var hits = matches.OrderBy(m => m.Rank).Take(limit).Select(m => m.Entry.Hit).ToList();
        return new CatalogSearchResult(hits, matches.Count);
    }

    private static bool InScope(Entry entry, CatalogScope scope) => scope switch
    {
        CatalogScope.Tables => !entry.Hit.IsColumn,
        CatalogScope.Columns => entry.Hit.IsColumn,
        _ => true
    };

    private static bool Matches(Entry entry, string[] tokens, CatalogSearchField field)
    {
        foreach (var token in tokens)
        {
            var found = field switch
            {
                CatalogSearchField.Name => entry.FullName.Contains(token, StringComparison.Ordinal),
                CatalogSearchField.Description => entry.Description.Contains(token, StringComparison.Ordinal),
                _ => entry.FullName.Contains(token, StringComparison.Ordinal)
                     || entry.Description.Contains(token, StringComparison.Ordinal)
            };

            if (!found)
            {
                return false;
            }
        }

        return true;
    }

    private static int Rank(Entry entry, string[] tokens)
    {
        if (tokens.Length == 1 && entry.Name == tokens[0])
        {
            return 0;
        }

        if (entry.Name.StartsWith(tokens[0], StringComparison.Ordinal))
        {
            return 1;
        }

        return tokens.All(t => entry.FullName.Contains(t, StringComparison.Ordinal)) ? 2 : 3;
    }

    /// <param name="Name">Nome curto normalizado (só a tabela ou só a coluna).</param>
    private sealed record Entry(CatalogSearchHit Hit, string Name, string FullName, string Description)
    {
        public string Name { get; } = TextNormalizer.Normalize(Name);
        public string FullName { get; } = TextNormalizer.Normalize(FullName);
        public string Description { get; } = TextNormalizer.Normalize(Description);
    }
}
