using BancoPronto.Core.Models;
using BancoPronto.Core.Scanning;

namespace BancoPronto.Core.Tests;

public sealed class BackupScannerTests : IDisposable
{
    private readonly BackupFolder _folder = new();
    private readonly BackupScanner _scanner = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void Identifica_o_completo_e_ordena_as_modificacoes_por_data()
    {
        _folder.Add("DEMO_20261003_1015_M.backup");
        _folder.Add("DEMO_20261001_0800_C.backup");
        _folder.Add("DEMO_20261002_0900_M.backup");

        var result = _scanner.Scan(_folder.Backups);

        Assert.True(result.CanPrepare);
        Assert.Empty(result.Issues);
        Assert.Equal("DEMO_20261001_0800_C.backup", result.Full!.FileName);
        Assert.Equal(
            ["DEMO_20261001_0800_C.backup", "DEMO_20261002_0900_M.backup", "DEMO_20261003_1015_M.backup"],
            result.Sequence.Select(b => b.FileName));
    }

    [Fact]
    public void Usa_o_completo_mais_recente_e_ignora_o_que_ja_esta_nele()
    {
        _folder.Add("DEMO_20260901_0800_C.backup");
        _folder.Add("DEMO_20260915_0800_M.backup");
        _folder.Add("DEMO_20261001_0800_C.backup");
        _folder.Add("DEMO_20261002_0900_M.backup");

        var result = _scanner.Scan(_folder.Backups);

        Assert.True(result.CanPrepare);
        Assert.Equal("DEMO_20261001_0800_C.backup", result.Full!.FileName);
        Assert.Equal(["DEMO_20261002_0900_M.backup"], result.Modifications.Select(b => b.FileName));
        Assert.Equal(["DEMO_20260901_0800_C.backup", "DEMO_20260915_0800_M.backup"], result.Ignored.Select(b => b.FileName));
        Assert.All(result.Issues, issue => Assert.Equal(IssueSeverity.Info, issue.Severity));
    }

    [Fact]
    public void Sem_backup_completo_bloqueia_a_preparacao()
    {
        _folder.Add("DEMO_20261002_0900_M.backup");

        var result = _scanner.Scan(_folder.Backups);

        Assert.False(result.CanPrepare);
        Assert.Null(result.Full);
        Assert.Contains(result.Issues, i => i.Severity == IssueSeverity.Error && i.Message.Contains("completo"));
    }

    [Fact]
    public void Bancos_diferentes_na_mesma_pasta_bloqueiam_a_preparacao()
    {
        _folder.Add("DEMO_20261001_0800_C.backup");
        _folder.Add("OUTRO_20261002_0900_M.backup", code: "OUTRO");

        var result = _scanner.Scan(_folder.Backups);

        Assert.False(result.CanPrepare);
        Assert.Contains(result.Issues, i => i.Severity == IssueSeverity.Error && i.Message.Contains("bancos diferentes"));
    }

    [Fact]
    public void Nome_fora_do_padrao_gera_aviso_sem_bloquear()
    {
        _folder.Add("DEMO_20261001_0800_C.backup");
        _folder.Add("copia de seguranca.backup");

        var result = _scanner.Scan(_folder.Backups);

        Assert.True(result.CanPrepare);
        var warning = Assert.Single(result.Issues);
        Assert.Equal(IssueSeverity.Warning, warning.Severity);
        Assert.Equal("copia de seguranca.backup", warning.FileName);
    }

    [Fact]
    public void Pasta_vazia_bloqueia_e_pasta_inexistente_falha()
    {
        var empty = _scanner.Scan(_folder.Backups);
        Assert.False(empty.CanPrepare);
        Assert.Equal(IssueSeverity.Error, Assert.Single(empty.Issues).Severity);

        Assert.Throws<DirectoryNotFoundException>(() => _scanner.Scan(Path.Combine(_folder.Root, "nao-existe")));
    }
}
