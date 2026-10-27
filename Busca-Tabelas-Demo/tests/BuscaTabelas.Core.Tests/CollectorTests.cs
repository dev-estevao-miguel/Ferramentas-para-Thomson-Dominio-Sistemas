using System.Text;
using BuscaTabelas.Core.Catalog;
using BuscaTabelas.Core.Collector;

namespace BuscaTabelas.Core.Tests;

public sealed class CollectorTests : IDisposable
{
    private readonly CatalogFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Theory]
    [InlineData("DSN EXEMPLO", "'DSN EXEMPLO'")]
    [InlineData("O'Brien", "'O''Brien'")]
    [InlineData("a\u2019b", "'a\u2019\u2019b'")]
    [InlineData("$(calc)", "'$(calc)'")]
    public void Quote_gera_literal_seguro(string value, string expected) => Assert.Equal(expected, CollectorCommand.Quote(value));

    [Fact]
    public void Comando_passa_parametros_literais_e_limita_o_timeout()
    {
        var request = new CollectorRequest(CollectorMode.Exportar, "DSN_EXEMPLO", "BT'X", "DEMO", 999, @"C:\Dados\Catálogos");

        var command = CollectorCommand.Build(request, @"C:\Coletor\BT.ps1");

        Assert.Contains(@"& 'C:\Coletor\BT.ps1' -Modo Exportar -Dsn 'DSN_EXEMPLO' -Usuario 'BT''X' -Esquema 'DEMO' -Timeout 300 -Saida 'C:\Dados\Catálogos'", command);
        Assert.Contains("Read-Host", command);
        Assert.Contains("exit $code", command);
    }

    [Fact]
    public void Encode_usa_utf16_em_base64()
    {
        var encoded = CollectorCommand.Encode("Write-Host 'ç'");
        Assert.Equal("Write-Host 'ç'", Encoding.Unicode.GetString(Convert.FromBase64String(encoded)));
    }

    [Fact]
    public void Coletor_esta_embutido_com_as_consultas()
    {
        var names = typeof(CollectorService).Assembly.GetManifestResourceNames();
        Assert.Contains("BuscaTabelas.Coletor.BT.ps1", names);
        Assert.Contains("BuscaTabelas.Coletor.sql.tabelas.sql", names);
        Assert.Contains("BuscaTabelas.Coletor.sql.colunas.sql", names);
    }

    [Fact]
    public void Localiza_exportacoes_da_mais_recente_para_a_mais_antiga()
    {
        foreach (var folder in new[] { "20261014-111850-f6003738", "20261015-080000-aaaaaaaa", "sem-catalogo" })
        {
            Directory.CreateDirectory(Path.Combine(_fixture.Folder, folder));
        }

        _fixture.Write(Path.Combine("20261014-111850-f6003738", CatalogLocator.CatalogFileName));
        _fixture.Write(Path.Combine("20261015-080000-aaaaaaaa", CatalogLocator.CatalogFileName));

        var catalogs = CatalogLocator.List(_fixture.Folder);

        Assert.Equal(["20261015-080000-aaaaaaaa", "20261014-111850-f6003738"], catalogs.Select(c => c.FolderName));
        Assert.Equal(new DateTime(2026, 10, 15, 8, 0, 0), catalogs[0].ExportedAt);
        Assert.Empty(CatalogLocator.List(Path.Combine(_fixture.Folder, "nao-existe")));
    }
}
