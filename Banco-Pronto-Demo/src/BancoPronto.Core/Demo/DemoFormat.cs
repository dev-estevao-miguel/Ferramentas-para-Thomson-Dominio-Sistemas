using BancoPronto.Core.Models;

namespace BancoPronto.Core.Demo;

/// <summary>
/// Formato dos arquivos fictícios. Um backup de demonstração é um texto "chave=valor" que começa pelo
/// marcador; o "banco" preparado é outro texto com a lista dos backups aplicados, em ordem.
/// </summary>
public static class DemoFormat
{
    /// <summary>Primeira linha obrigatória de todo backup de demonstração.</summary>
    public const string BackupMarker = "# Banco Pronto Demo";

    public const string DatabaseFileName = "banco-demo.txt";

    public const string DatabaseHeader = "# Banco Pronto Demo - banco preparado (fictício)";

    private const string AppliedPrefix = "aplicado=";

    public static string AppliedLine(BackupFile backup) => AppliedPrefix + backup.FileName;

    /// <summary>Nomes dos backups aplicados ao banco, na ordem em que foram aplicados.</summary>
    public static IReadOnlyList<string> ReadApplied(string databasePath) =>
        File.ReadLines(databasePath)
            .Where(line => line.StartsWith(AppliedPrefix, StringComparison.Ordinal))
            .Select(line => line[AppliedPrefix.Length..])
            .ToList();

    /// <summary>Campos "chave=valor" de um backup extraído (linhas com # são comentários).</summary>
    public static IReadOnlyDictionary<string, string> ReadFields(IEnumerable<string> lines)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines)
        {
            var separator = line.IndexOf('=');
            if (line.StartsWith('#') || separator <= 0)
            {
                continue;
            }

            fields[line[..separator].Trim()] = line[(separator + 1)..].Trim();
        }

        return fields;
    }
}
