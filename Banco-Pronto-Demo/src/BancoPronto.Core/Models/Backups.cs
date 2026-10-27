namespace BancoPronto.Core.Models;

public enum BackupKind
{
    /// <summary>Backup completo (sufixo _C): ponto de partida da preparação.</summary>
    Full,

    /// <summary>Modificação incremental (sufixo _M): aplicada depois do completo, em ordem cronológica.</summary>
    Modification
}

/// <summary>Partes do nome <c>&lt;BANCO&gt;_&lt;aaaaMMdd&gt;_&lt;HHmm&gt;_&lt;C|M&gt;.backup</c>.</summary>
public sealed record BackupFileName(string DatabaseCode, DateTime Timestamp, BackupKind Kind);

/// <summary>Arquivo de backup reconhecido na pasta, com o tamanho e a data de gravação lidos na verificação.</summary>
/// <param name="LastWriteUtc">Usado na validação para confirmar que o original não foi alterado.</param>
public sealed record BackupFile(
    string Path,
    string DatabaseCode,
    DateTime Timestamp,
    BackupKind Kind,
    long SizeBytes,
    DateTime LastWriteUtc)
{
    public string FileName => System.IO.Path.GetFileName(Path);
}
