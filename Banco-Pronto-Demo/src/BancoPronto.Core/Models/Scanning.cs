namespace BancoPronto.Core.Models;

public enum IssueSeverity
{
    /// <summary>Informação: algo foi ignorado de propósito (ex.: modificação anterior ao completo).</summary>
    Info,

    /// <summary>Aviso: não impede a preparação.</summary>
    Warning,

    /// <summary>Erro: a preparação fica bloqueada até a pasta ser corrigida.</summary>
    Error
}

/// <param name="FileName">Arquivo relacionado, quando houver.</param>
public sealed record ScanIssue(IssueSeverity Severity, string Message, string? FileName = null);

/// <summary>Resultado da verificação de uma pasta: o que será aplicado, em qual ordem, e o que foi ignorado.</summary>
public sealed record BackupScanResult
{
    public required string Folder { get; init; }

    /// <summary>Backup completo usado como base (o mais recente da pasta).</summary>
    public BackupFile? Full { get; init; }

    /// <summary>Modificações posteriores ao completo, em ordem cronológica.</summary>
    public IReadOnlyList<BackupFile> Modifications { get; init; } = [];

    /// <summary>Arquivos reconhecidos que não entram na preparação (completos antigos, modificações anteriores).</summary>
    public IReadOnlyList<BackupFile> Ignored { get; init; } = [];

    public IReadOnlyList<ScanIssue> Issues { get; init; } = [];

    public bool HasErrors => Issues.Any(i => i.Severity == IssueSeverity.Error);

    public bool CanPrepare => Full is not null && !HasErrors;

    /// <summary>Ordem de aplicação: o completo e depois as modificações.</summary>
    public IReadOnlyList<BackupFile> Sequence => Full is null ? [] : [Full, .. Modifications];
}
