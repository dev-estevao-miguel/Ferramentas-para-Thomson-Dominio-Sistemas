using BancoPronto.Core.Abstractions;
using BancoPronto.Core.Models;

namespace BancoPronto.Core.Demo;

/// <summary>
/// Extração simulada: confere o marcador do arquivo fictício e copia o conteúdo para a pasta de trabalho.
/// O arquivo original é apenas lido.
/// </summary>
public sealed class DemoBackupExtractor : IBackupExtractor
{
    private readonly DemoOptions _options;

    public DemoBackupExtractor(DemoOptions options)
    {
        _options = options;
    }

    public async Task<string> ExtractAsync(BackupFile backup, string workFolder, CancellationToken cancellationToken)
    {
        await Task.Delay(_options.StepDelay, cancellationToken);

        var lines = await File.ReadAllLinesAsync(backup.Path, cancellationToken);
        if (lines.Length == 0 || !lines[0].StartsWith(DemoFormat.BackupMarker, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"{backup.FileName} não é um backup de demonstração do Banco Pronto.");
        }

        var folder = Path.Combine(workFolder, "extraidos");
        Directory.CreateDirectory(folder);
        var target = Path.Combine(folder, Path.GetFileNameWithoutExtension(backup.FileName) + ".txt");
        await File.WriteAllLinesAsync(target, lines, cancellationToken);
        return target;
    }
}
