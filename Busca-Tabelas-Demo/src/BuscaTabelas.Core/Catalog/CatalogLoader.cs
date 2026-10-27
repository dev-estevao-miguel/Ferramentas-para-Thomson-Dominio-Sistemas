using System.Globalization;
using System.Text.Json;

namespace BuscaTabelas.Core.Catalog;

/// <summary>Arquivo que não é um catálogo válido do BT. A mensagem já é adequada para o usuário.</summary>
public sealed class CatalogFormatException(string message, Exception? inner = null) : Exception(message, inner);

public interface ICatalogLoader
{
    /// <exception cref="CatalogFormatException">Formato desconhecido, campos ausentes ou vínculos inconsistentes.</exception>
    /// <exception cref="IOException">Arquivo inacessível.</exception>
    DatabaseCatalog Load(string path);
}

/// <summary>
/// Lê o JSON gerado pelo coletor (tools/coletor/BT.ps1) e confere a integridade antes de indexar:
/// sem tabelas ou colunas duplicadas, sem colunas órfãs e sem códigos sem coluna.
/// </summary>
public sealed class CatalogLoader : ICatalogLoader
{
    public const string Format = "bt-catalogo-v1";

    public DatabaseCatalog Load(string path)
    {
        var fullPath = Path.GetFullPath(path);
        using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, FileOptions.SequentialScan);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(stream);
        }
        catch (JsonException ex)
        {
            throw new CatalogFormatException("O arquivo não é um JSON válido. Exporte o catálogo novamente.", ex);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || ReadString(root, "formato") != Format)
            {
                throw new CatalogFormatException($"Formato de catálogo não reconhecido. Esperado: {Format}, gerado pelo coletor do BT.");
            }

            var metadata = ReadMetadata(root);
            var tables = ReadTables(RequireArray(root, "tabelas"));
            var tablesById = tables.ToDictionary(t => t.Id);
            var columnCount = ReadColumns(RequireArray(root, "colunas"), tablesById);
            var codeCount = ReadCodes(RequireArray(root, "valores"), tables);

            return new DatabaseCatalog(fullPath, metadata, tables, columnCount, codeCount);
        }
    }

    private static CatalogMetadata ReadMetadata(JsonElement root)
    {
        var generatedText = ReadString(root, "gerado_em");
        DateTimeOffset? generatedAt = DateTimeOffset.TryParse(generatedText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : null;

        // O PowerShell grava "servidor" como lista; aceita também um objeto único.
        var server = root.TryGetProperty("servidor", out var serverElement) ? serverElement : default;
        if (server.ValueKind == JsonValueKind.Array)
        {
            server = server.GetArrayLength() > 0 ? server[0] : default;
        }

        var serverInfo = server.ValueKind == JsonValueKind.Object
            ? new CatalogServerInfo(ReadString(server, "banco"), ReadString(server, "versao_servidor"))
            : new CatalogServerInfo(null, null);

        var warnings = root.TryGetProperty("avisos", out var warningsElement) && warningsElement.ValueKind == JsonValueKind.Array
            ? warningsElement.EnumerateArray().Select(w => AsString(w) ?? string.Empty).Where(w => w.Length > 0).ToList()
            : [];

        return new CatalogMetadata(
            Format,
            generatedAt,
            generatedText,
            serverInfo,
            ReadString(root, "esquema"),
            ReadString(root, "escopo"),
            ReadString(root, "status_valores"),
            warnings);
    }

    /// <returns>Tabelas na ordem do arquivo.</returns>
    private static List<CatalogTable> ReadTables(JsonElement array)
    {
        var tables = new List<CatalogTable>();
        var ids = new HashSet<int>();
        var identities = new HashSet<(string, string)>();
        foreach (var item in array.EnumerateArray())
        {
            var table = new CatalogTable
            {
                Id = RequireInt(item, "tabela_id"),
                Schema = RequireString(item, "esquema"),
                Name = RequireString(item, "tabela"),
                Type = ReadString(item, "tipo") ?? string.Empty,
                Description = ReadString(item, "descricao") ?? string.Empty
            };

            if (!ids.Add(table.Id) || !identities.Add((table.Schema, table.Name)))
            {
                throw new CatalogFormatException($"Tabela duplicada no catálogo: {table.FullName}.");
            }

            tables.Add(table);
        }

        return tables;
    }

    private static int ReadColumns(JsonElement array, Dictionary<int, CatalogTable> tables)
    {
        var byTable = new Dictionary<int, List<CatalogColumn>>();
        var names = new HashSet<(string, string, string)>();
        var count = 0;

        foreach (var item in array.EnumerateArray())
        {
            var column = new CatalogColumn
            {
                TableId = RequireInt(item, "tabela_id"),
                Id = RequireInt(item, "coluna_id"),
                Schema = RequireString(item, "esquema"),
                TableName = RequireString(item, "tabela"),
                Name = RequireString(item, "coluna"),
                Type = ReadString(item, "tipo") ?? string.Empty,
                Size = ReadLong(item, "tamanho_ou_precisao"),
                Scale = ReadLong(item, "escala"),
                IsPrimaryKey = ReadFlag(item, "chave_primaria") == true,
                AllowsNull = ReadFlag(item, "aceita_nulo"),
                DefaultValue = ReadString(item, "valor_padrao"),
                Description = ReadString(item, "descricao") ?? string.Empty
            };

            if (!tables.TryGetValue(column.TableId, out var table) || table.Schema != column.Schema || table.Name != column.TableName)
            {
                throw new CatalogFormatException($"Coluna sem tabela correspondente: {column.FullName}.");
            }

            if (!names.Add((column.Schema, column.TableName, column.Name)))
            {
                throw new CatalogFormatException($"Nome de coluna duplicado na mesma tabela: {column.FullName}.");
            }

            column.Table = table;
            if (!byTable.TryGetValue(table.Id, out var list))
            {
                byTable[table.Id] = list = [];
            }

            list.Add(column);
            count++;
        }

        foreach (var (tableId, columns) in byTable)
        {
            columns.Sort((a, b) => a.Id.CompareTo(b.Id));
            if (columns.Select(c => c.Id).Distinct().Count() != columns.Count)
            {
                throw new CatalogFormatException($"Identificador de coluna repetido na tabela {tables[tableId].FullName}.");
            }

            tables[tableId].Columns = columns;
        }

        return count;
    }

    private static int ReadCodes(JsonElement array, List<CatalogTable> tables)
    {
        var columns = tables
            .SelectMany(t => t.Columns)
            .ToDictionary(c => (c.Schema, c.TableName, c.Name));
        var codes = new Dictionary<CatalogColumn, List<CatalogCode>>();
        var count = 0;

        foreach (var item in array.EnumerateArray())
        {
            var key = (RequireString(item, "esquema"), RequireString(item, "tabela"), RequireString(item, "coluna"));
            if (!columns.TryGetValue(key, out var column))
            {
                throw new CatalogFormatException($"Código sem coluna correspondente: {key.Item1}.{key.Item2}.{key.Item3}.");
            }

            if (!codes.TryGetValue(column, out var list))
            {
                codes[column] = list = [];
            }

            list.Add(new CatalogCode(ReadString(item, "valor") ?? string.Empty, ReadString(item, "descricao") ?? string.Empty));
            count++;
        }

        foreach (var (column, list) in codes)
        {
            column.Codes = list;
        }

        return count;
    }

    private static JsonElement RequireArray(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value
            : throw new CatalogFormatException($"Lista ausente ou inválida no catálogo: {name}.");

    private static string RequireString(JsonElement item, string name) =>
        ReadString(item, name) is { Length: > 0 } value
            ? value
            : throw new CatalogFormatException($"Campo obrigatório ausente no catálogo: {name}.");

    private static int RequireInt(JsonElement item, string name) =>
        ReadLong(item, name) is { } value and >= int.MinValue and <= int.MaxValue
            ? (int)value
            : throw new CatalogFormatException($"Campo obrigatório ausente ou inválido no catálogo: {name}.");

    private static string? ReadString(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) ? AsString(value) : null;

    /// <summary>Texto, número ou booleano como texto; nulo para null/ausente.</summary>
    private static string? AsString(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => null
    };

    private static long? ReadLong(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out var number) => number,
            JsonValueKind.String when long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) => number,
            _ => null
        };
    }

    /// <summary>SQL Anywhere usa 'Y'/'N'; aceita também booleanos e 1/0.</summary>
    private static bool? ReadFlag(JsonElement item, string name) => ReadString(item, name)?.Trim().ToUpperInvariant() switch
    {
        "Y" or "S" or "TRUE" or "1" => true,
        "N" or "FALSE" or "0" => false,
        _ => null
    };
}
