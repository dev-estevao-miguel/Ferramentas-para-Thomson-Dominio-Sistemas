using BancoPronto.Core.Models;

namespace BancoPronto.Core.Abstractions;

/// <summary>Analisa uma pasta e decide o que será aplicado e em qual ordem. Não altera nenhum arquivo.</summary>
public interface IBackupScanner
{
    /// <exception cref="DirectoryNotFoundException">A pasta não existe.</exception>
    BackupScanResult Scan(string folder);
}

/// <summary>Extrai um backup para a pasta de trabalho, sem tocar no arquivo original.</summary>
public interface IBackupExtractor
{
    /// <returns>Caminho do conteúdo extraído.</returns>
    /// <exception cref="InvalidDataException">O arquivo não pôde ser extraído.</exception>
    Task<string> ExtractAsync(BackupFile backup, string workFolder, CancellationToken cancellationToken);
}

/// <summary>Aplica o conteúdo extraído de um backup ao banco em preparação.</summary>
public interface ILogProcessor
{
    /// <exception cref="InvalidDataException">O conteúdo não corresponde ao banco ou à ordem esperada.</exception>
    Task ApplyAsync(string databasePath, BackupFile backup, string extractedPath, CancellationToken cancellationToken);
}

/// <summary>Confere o banco preparado e a integridade dos backups originais.</summary>
public interface IValidationService
{
    Task<ValidationReport> ValidateAsync(string databasePath, BackupScanResult backups, CancellationToken cancellationToken);
}

/// <summary>Orquestra extração, aplicação e validação, reportando o progresso de cada etapa.</summary>
public interface IPreparationService
{
    /// <exception cref="InvalidOperationException">A verificação da pasta encontrou erros.</exception>
    /// <exception cref="OperationCanceledException">Cancelado pelo usuário.</exception>
    Task<PreparationResult> PrepareAsync(
        BackupScanResult backups,
        IProgress<PreparationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
