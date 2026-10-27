using BuscaTabelas.Core.Catalog;

namespace BuscaTabelas.Core.Tests;

public sealed class ExportTests : IDisposable
{
    private readonly CatalogFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void Select_escapa_identificadores_e_segue_a_ordem_do_banco()
    {
        var table = _fixture.Load().FindTable(1)!;

        Assert.Equal("SELECT\n    \"sit\"\"uacao\"\nFROM \"demo\".\"pessoas\";", SelectBuilder.Build(table, [table.Columns[0]]));
        Assert.Equal(
            "SELECT\n    \"sit\"\"uacao\",\n    \"nome\"\nFROM \"demo\".\"pessoas\";",
            SelectBuilder.Build(table, [table.Columns[1], table.Columns[0]]));
    }

    [Fact]
    public void Select_recusa_coluna_de_outra_tabela_ou_lista_vazia()
    {
        var catalog = _fixture.Load();
        var table = catalog.FindTable(1)!;

        Assert.Throws<ArgumentException>(() => SelectBuilder.Build(table, []));
        Assert.Throws<ArgumentException>(() => SelectBuilder.Build(table, [catalog.FindTable(2)!.Columns[0]]));
    }

    [Fact]
    public void Contexto_escapa_markdown_e_nao_inclui_outra_tabela()
    {
        var catalog = _fixture.Load();

        var context = AiContextBuilder.Build(catalog, catalog.FindTable(1)!);

        Assert.Contains("Situação \\| vínculo<br>ativo", context);
        Assert.Contains("| 1 | Ativo |", context);
        Assert.Contains("| sit\"uacao | integer | 4 | 0 | Sim | Não | 1 |", context);
        Assert.DoesNotContain("outro.pessoas", context);
        Assert.Contains("Chaves estrangeiras e regras de junção não foram extraídas", context);
        Assert.DoesNotContain("\r", context);
    }

    [Fact]
    public void Contexto_avisa_quando_nao_ha_codigos()
    {
        var catalog = _fixture.Load();

        var context = AiContextBuilder.Build(catalog, catalog.FindTable(2)!);

        Assert.Contains("Nenhum código documentado", context);
    }

    [Fact]
    public void Sugere_nome_de_arquivo_seguro()
    {
        var table = _fixture.Load().FindTable(1)!;
        Assert.Equal("demo.pessoas.md", AiContextBuilder.SuggestFileName(table));
    }
}
