using System.Globalization;
using BancoPronto.Core.Abstractions;
using BancoPronto.Core.Models;

namespace BancoPronto.Core.Scanning;

/// <summary>
/// Verifica a pasta dos backups: usa o completo mais recente como base e, depois dele, as modificações
/// em ordem cronológica. Situações ambíguas bloqueiam a preparação em vez de escolher um arquivo em silêncio.
/// </summary>
public sealed class BackupScanner : IBackupScanner
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("pt-BR");

    public BackupScanResult Scan(string folder)
    {
        if (!Directory.Exists(folder))
        {
            throw new DirectoryNotFoundException($"Pasta não encontrada: {folder}");
        }

        var issues = new List<ScanIssue>();
        var recognized = new List<BackupFile>();

        foreach (var file in new DirectoryInfo(folder)
                     .EnumerateFiles("*" + BackupFileNameParser.Extension)
                     .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (BackupFileNameParser.Parse(file.Name) is not { } name)
            {
                issues.Add(new ScanIssue(IssueSeverity.Warning,
                    $"Nome fora do padrão {BackupFileNameParser.PatternDescription}; arquivo ignorado.", file.Name));
                continue;
            }

            recognized.Add(new BackupFile(file.FullName, name.DatabaseCode, name.Timestamp, name.Kind, file.Length, file.LastWriteTimeUtc));
        }

        if (recognized.Count == 0)
        {
            issues.Add(new ScanIssue(IssueSeverity.Error, "Nenhum backup reconhecido nesta pasta."));
            return new BackupScanResult { Folder = folder, Issues = issues };
        }

        var codes = recognized.Select(b => b.DatabaseCode).Distinct().ToList();
        if (codes.Count > 1)
        {
            issues.Add(new ScanIssue(IssueSeverity.Error,
                $"A pasta mistura backups de bancos diferentes ({string.Join(", ", codes)}). Separe-os em pastas próprias."));
        }

        var fulls = recognized.Where(b => b.Kind == BackupKind.Full).OrderBy(b => b.Timestamp).ToList();
        if (fulls.Count == 0)
        {
            issues.Add(new ScanIssue(IssueSeverity.Error,
                "Nenhum backup completo (_C) encontrado. As modificações dependem de um completo."));
            return new BackupScanResult { Folder = folder, Ignored = recognized, Issues = issues };
        }

        var full = fulls[^1];
        var ignored = new List<BackupFile>();

        if (fulls.Count(f => f.Timestamp == full.Timestamp) > 1)
        {
            issues.Add(new ScanIssue(IssueSeverity.Error,
                $"Há mais de um backup completo de {Describe(full.Timestamp)}: não é possível escolher a base com segurança."));
        }

        foreach (var older in fulls.Where(f => f.Timestamp < full.Timestamp))
        {
            ignored.Add(older);
            issues.Add(new ScanIssue(IssueSeverity.Info, "Backup completo mais antigo; será usado o mais recente.", older.FileName));
        }

        var modifications = recognized.Where(b => b.Kind == BackupKind.Modification).OrderBy(b => b.Timestamp).ToList();
        foreach (var previous in modifications.Where(m => m.Timestamp <= full.Timestamp))
        {
            ignored.Add(previous);
            issues.Add(new ScanIssue(IssueSeverity.Info, "Modificação anterior ao backup completo; já está incluída nele.", previous.FileName));
        }

        // Mesma data/hora só é possível com nomes que diferem em maiúsculas (sistemas de arquivos que as diferenciam).
        var toApply = modifications.Where(m => m.Timestamp > full.Timestamp).ToList();
        foreach (var duplicate in toApply.GroupBy(m => m.Timestamp).Where(g => g.Count() > 1))
        {
            issues.Add(new ScanIssue(IssueSeverity.Error,
                $"Há {duplicate.Count()} modificações de {Describe(duplicate.Key)}: a ordem de aplicação seria ambígua."));
        }

        return new BackupScanResult
        {
            Folder = folder,
            Full = full,
            Modifications = toApply,
            Ignored = ignored.OrderBy(b => b.Timestamp).ToList(),
            Issues = issues
        };
    }

    private static string Describe(DateTime timestamp) => timestamp.ToString("dd/MM/yyyy HH:mm", Culture);
}
