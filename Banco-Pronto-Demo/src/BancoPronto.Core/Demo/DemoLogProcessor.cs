using BancoPronto.Core.Abstractions;
using BancoPronto.Core.Models;

namespace BancoPronto.Core.Demo;

/// <summary>
/// Aplicação simulada: o completo cria o banco e cada modificação acrescenta uma linha, na ordem recebida.
/// Recusa conteúdo de outro banco e modificação aplicada antes do completo.
/// </summary>
public sealed class DemoLogProcessor : ILogProcessor
{
    private readonly DemoOptions _options;

    public DemoLogProcessor(DemoOptions options)
    {
        _options = options;
    }

    public async Task ApplyAsync(string databasePath, BackupFile backup, string extractedPath, CancellationToken cancellationToken)
    {
        await Task.Delay(_options.StepDelay, cancellationToken);

        var fields = DemoFormat.ReadFields(await File.ReadAllLinesAsync(extractedPath, cancellationToken));
        if (!fields.TryGetValue("banco", out var code) || !string.Equals(code, backup.DatabaseCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"{backup.FileName}: o conteúdo não pertence ao banco {backup.DatabaseCode}.");
        }

        if (backup.Kind == BackupKind.Full)
        {
            if (File.Exists(databasePath))
            {
                throw new InvalidDataException("O banco já foi restaurado nesta preparação: só pode haver um backup completo.");
            }

            string[] lines = [DemoFormat.DatabaseHeader, $"banco={backup.DatabaseCode}", DemoFormat.AppliedLine(backup)];
            await File.WriteAllLinesAsync(databasePath, lines, cancellationToken);
            return;
        }

        if (!File.Exists(databasePath))
        {
            throw new InvalidDataException($"{backup.FileName}: a modificação só pode ser aplicada depois do backup completo.");
        }

        string[] appended = [DemoFormat.AppliedLine(backup)];
        await File.AppendAllLinesAsync(databasePath, appended, cancellationToken);
    }
}
