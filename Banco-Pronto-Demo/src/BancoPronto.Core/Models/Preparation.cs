namespace BancoPronto.Core.Models;

public enum PreparationStage
{
    Extracting,
    ApplyingLogs,
    Validating,
    Completed
}

/// <param name="Percent">De 0 a 100, calculado pelas etapas concluídas.</param>
public sealed record PreparationProgress(PreparationStage Stage, int Percent, string Message);

/// <param name="Checks">Verificações aprovadas, em linguagem do usuário.</param>
/// <param name="Problems">Verificações reprovadas; vazio quando o banco é válido.</param>
public sealed record ValidationReport(IReadOnlyList<string> Checks, IReadOnlyList<string> Problems)
{
    public bool IsValid => Problems.Count == 0;
}

/// <param name="DatabasePath">Banco preparado; nulo quando a preparação falhou antes de gerá-lo.</param>
/// <param name="WorkFolder">Pasta de trabalho própria: os backups originais nunca são alterados.</param>
/// <param name="Error">Mensagem amigável quando <paramref name="Succeeded"/> é falso.</param>
public sealed record PreparationResult(
    bool Succeeded,
    string? DatabasePath,
    string WorkFolder,
    ValidationReport? Validation,
    TimeSpan Duration,
    string? Error = null);
