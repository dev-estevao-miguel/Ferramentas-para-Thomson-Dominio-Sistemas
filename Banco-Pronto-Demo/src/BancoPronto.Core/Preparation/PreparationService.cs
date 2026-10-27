using System.Diagnostics;
using System.Globalization;
using BancoPronto.Core.Abstractions;
using BancoPronto.Core.Demo;
using BancoPronto.Core.Models;

namespace BancoPronto.Core.Preparation;

/// <summary>
/// Fluxo de preparação: para cada backup da sequência (completo e depois modificações), extrai e aplica;
/// ao final, valida. Tudo acontece numa pasta de trabalho nova, então o processo pode ser repetido
/// sem afetar os originais. Cada etapa é uma interface: trocar a simulação por uma implementação real
/// não muda este orquestrador nem a interface gráfica.
/// </summary>
public sealed class PreparationService : IPreparationService
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("pt-BR");

    private readonly IBackupExtractor _extractor;
    private readonly ILogProcessor _logProcessor;
    private readonly IValidationService _validator;
    private readonly DemoOptions _options;

    public PreparationService(IBackupExtractor extractor, ILogProcessor logProcessor, IValidationService validator, DemoOptions options)
    {
        _extractor = extractor;
        _logProcessor = logProcessor;
        _validator = validator;
        _options = options;
    }

    public async Task<PreparationResult> PrepareAsync(
        BackupScanResult backups,
        IProgress<PreparationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!backups.CanPrepare || backups.Full is null)
        {
            throw new InvalidOperationException("A verificação da pasta encontrou problemas; corrija-os antes de preparar o banco.");
        }

        var stopwatch = Stopwatch.StartNew();
        var sequence = backups.Sequence;
        var totalSteps = sequence.Count * 2 + 1;
        var completedSteps = 0;

        void Report(PreparationStage stage, string message) =>
            progress?.Report(new PreparationProgress(stage, completedSteps * 100 / totalSteps, message));

        var workFolder = Path.Combine(_options.WorkRoot, $"{backups.Full.DatabaseCode}_{DateTime.Now:yyyyMMdd_HHmmss_fff}");
        Directory.CreateDirectory(workFolder);
        var databasePath = Path.Combine(workFolder, DemoFormat.DatabaseFileName);

        try
        {
            foreach (var backup in sequence)
            {
                cancellationToken.ThrowIfCancellationRequested();

                Report(PreparationStage.Extracting, $"Extraindo {backup.FileName}…");
                var extracted = await _extractor.ExtractAsync(backup, workFolder, cancellationToken);
                completedSteps++;

                Report(PreparationStage.ApplyingLogs, backup.Kind == BackupKind.Full
                    ? $"Restaurando o backup completo de {Describe(backup.Timestamp)}…"
                    : $"Aplicando a modificação de {Describe(backup.Timestamp)}…");
                await _logProcessor.ApplyAsync(databasePath, backup, extracted, cancellationToken);
                completedSteps++;
            }

            Report(PreparationStage.Validating, "Validando o banco preparado…");
            var validation = await _validator.ValidateAsync(databasePath, backups, cancellationToken);
            completedSteps++;

            progress?.Report(new PreparationProgress(
                PreparationStage.Completed,
                100,
                validation.IsValid ? "Banco preparado." : "A validação encontrou problemas."));

            return new PreparationResult(
                validation.IsValid,
                databasePath,
                workFolder,
                validation,
                stopwatch.Elapsed,
                validation.IsValid ? null : string.Join(" ", validation.Problems));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return new PreparationResult(false, null, workFolder, null, stopwatch.Elapsed, ex.Message);
        }
    }

    private static string Describe(DateTime timestamp) => timestamp.ToString("dd/MM/yyyy HH:mm", Culture);
}
