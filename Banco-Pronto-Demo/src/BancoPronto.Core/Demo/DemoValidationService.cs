using BancoPronto.Core.Abstractions;
using BancoPronto.Core.Models;

namespace BancoPronto.Core.Demo;

/// <summary>
/// Validação final: o banco existe, começa pelo completo, recebeu as modificações na ordem esperada
/// e os backups originais continuam com o mesmo tamanho e a mesma data de gravação.
/// </summary>
public sealed class DemoValidationService : IValidationService
{
    private readonly DemoOptions _options;

    public DemoValidationService(DemoOptions options)
    {
        _options = options;
    }

    public async Task<ValidationReport> ValidateAsync(string databasePath, BackupScanResult backups, CancellationToken cancellationToken)
    {
        await Task.Delay(_options.StepDelay, cancellationToken);

        var checks = new List<string>();
        var problems = new List<string>();

        if (!File.Exists(databasePath))
        {
            problems.Add("O arquivo do banco não foi gerado.");
            return new ValidationReport(checks, problems);
        }

        checks.Add("Banco gerado na pasta de trabalho");

        var applied = DemoFormat.ReadApplied(databasePath);
        if (backups.Full is not null && applied.Count > 0 && applied[0] == backups.Full.FileName)
        {
            checks.Add("Backup completo restaurado primeiro");
        }
        else
        {
            problems.Add("O banco não começa pelo backup completo.");
        }

        if (applied.SequenceEqual(backups.Sequence.Select(b => b.FileName)))
        {
            var count = backups.Modifications.Count;
            checks.Add(count == 1
                ? "1 modificação aplicada em ordem cronológica"
                : $"{count} modificações aplicadas em ordem cronológica");
        }
        else
        {
            problems.Add("As modificações aplicadas não correspondem à sequência verificada.");
        }

        var changed = backups.Sequence.Where(b => !IsUnchanged(b)).Select(b => b.FileName).ToList();
        if (changed.Count == 0)
        {
            checks.Add("Backups originais preservados (tamanho e data de gravação inalterados)");
        }
        else
        {
            problems.Add("Backups originais alterados durante a preparação: " + string.Join(", ", changed));
        }

        return new ValidationReport(checks, problems);
    }

    private static bool IsUnchanged(BackupFile backup)
    {
        var info = new FileInfo(backup.Path);
        return info.Exists && info.Length == backup.SizeBytes && info.LastWriteTimeUtc == backup.LastWriteUtc;
    }
}
