using BancoPronto.Core.Models;
using BancoPronto.Core.Scanning;

namespace BancoPronto.Core.Tests;

public sealed class BackupFileNameParserTests
{
    [Theory]
    [InlineData("DEMO_20261001_0800_C.backup", "DEMO", 2026, 10, 1, 8, 0, BackupKind.Full)]
    [InlineData("DEMO_20261003_1015_M.backup", "DEMO", 2026, 10, 3, 10, 15, BackupKind.Modification)]
    [InlineData("loja7_20251231_2359_m.backup", "LOJA7", 2025, 12, 31, 23, 59, BackupKind.Modification)]
    public void Le_banco_data_hora_e_tipo(string fileName, string code, int year, int month, int day, int hour, int minute, BackupKind kind)
    {
        var parsed = BackupFileNameParser.Parse(fileName);

        Assert.NotNull(parsed);
        Assert.Equal(code, parsed.DatabaseCode);
        Assert.Equal(new DateTime(year, month, day, hour, minute, 0), parsed.Timestamp);
        Assert.Equal(kind, parsed.Kind);
    }

    [Theory]
    [InlineData("DEMO_20261001_0800_X.backup")]
    [InlineData("DEMO_20261001_0800_C.zip")]
    [InlineData("DEMO_20260231_0800_C.backup")]
    [InlineData("DEMO_20261001_2460_M.backup")]
    [InlineData("DEMO-20261001-0800-C.backup")]
    [InlineData("_20261001_0800_C.backup")]
    [InlineData("leia-me.txt")]
    public void Recusa_nome_fora_do_padrao_ou_data_inexistente(string fileName) =>
        Assert.Null(BackupFileNameParser.Parse(fileName));
}
