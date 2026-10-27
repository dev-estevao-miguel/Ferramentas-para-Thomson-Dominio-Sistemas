using System.Globalization;

namespace BuscaTabelas.Core.Catalog;

/// <summary>Uma exportação encontrada na pasta de catálogos.</summary>
/// <param name="ExportedAt">Data/hora da exportação, lida do nome da pasta (aaaaMMdd-HHmmss-id).</param>
public sealed record CatalogFileInfo(string Path, string FolderName, DateTime ExportedAt, long SizeBytes);

/// <summary>
/// Localiza as exportações do coletor: cada uma fica em <c>&lt;pasta&gt;\&lt;aaaaMMdd-HHmmss-id&gt;\catalogo.json</c>.
/// </summary>
public static class CatalogLocator
{
    public const string CatalogFileName = "catalogo.json";

    /// <returns>Da mais recente para a mais antiga; lista vazia se a pasta não existir.</returns>
    public static IReadOnlyList<CatalogFileInfo> List(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return [];
        }

        var result = new List<CatalogFileInfo>();
        foreach (var directory in new DirectoryInfo(folder).EnumerateDirectories())
        {
            var file = new FileInfo(Path.Combine(directory.FullName, CatalogFileName));
            if (file.Exists)
            {
                result.Add(new CatalogFileInfo(file.FullName, directory.Name, ParseExportDate(directory.Name) ?? file.LastWriteTime, file.Length));
            }
        }

        return result
            .OrderByDescending(c => c.ExportedAt)
            .ThenByDescending(c => c.FolderName, StringComparer.Ordinal)
            .ToList();
    }

    public static CatalogFileInfo? FindLatest(string folder) => List(folder).FirstOrDefault();

    private static DateTime? ParseExportDate(string folderName) =>
        folderName.Length >= 15
        && DateTime.TryParseExact(folderName[..15], "yyyyMMdd-HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
}
