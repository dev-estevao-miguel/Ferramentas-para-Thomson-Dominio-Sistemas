using BuscaTabelas.Core.Catalog;

namespace BuscaTabelas.Core.Tests;

/// <summary>O catálogo fictício de samples/ precisa abrir e funcionar sem nenhum banco.</summary>
public sealed class SampleCatalogTests
{
    private static readonly string SamplePath = Path.Combine(AppContext.BaseDirectory, "samples", "catalogo-demo.json");

    private static DatabaseCatalog Load() => new CatalogLoader().Load(SamplePath);

    [Fact]
    public void Catalogo_de_demonstracao_e_valido()
    {
        var catalog = Load();

        Assert.Equal("demo", catalog.Metadata.Schema);
        Assert.Equal("disponivel", catalog.Metadata.ValuesStatus);
        Assert.Equal(12, catalog.Tables.Count);
        Assert.Equal(69, catalog.ColumnCount);
        Assert.Equal(33, catalog.CodeCount);
        Assert.Equal(1, catalog.CountOfType("VIEW"));
        Assert.All(catalog.Tables, t => Assert.EndsWith("_DEMO", t.Name));
    }

    [Fact]
    public void Pesquisa_encontra_tabela_pelo_nome_e_coluna_pela_descricao()
    {
        var catalog = Load();

        var tables = catalog.Search(new CatalogQuery("clientes_demo", CatalogScope.Tables));
        Assert.Equal("demo.CLIENTES_DEMO", tables.Hits[0].FullName);

        var columns = catalog.Search(new CatalogQuery("salário base", CatalogScope.Columns, CatalogSearchField.Description));
        Assert.Equal(1, columns.Total);
        Assert.Equal("demo.FUNCIONARIOS_DEMO.SALARIO", columns.Hits[0].FullName);
    }

    [Fact]
    public void Gera_select_e_contexto_a_partir_do_catalogo_de_demonstracao()
    {
        var catalog = Load();
        var pedidos = catalog.Tables.Single(t => t.Name == "PEDIDOS_DEMO");
        var situacao = pedidos.Columns.Single(c => c.Name == "SITUACAO");

        Assert.Equal(["1", "2", "3"], situacao.Codes.Select(c => c.Value));
        Assert.Equal(
            "SELECT\n    \"ID_PEDIDO\",\n    \"SITUACAO\"\nFROM \"demo\".\"PEDIDOS_DEMO\";",
            SelectBuilder.Build(pedidos, [situacao, pedidos.Columns[0]]));

        var context = AiContextBuilder.Build(catalog, pedidos);
        Assert.Contains("| 3 | Cancelado |", context);
        Assert.Equal("demo.PEDIDOS_DEMO.md", AiContextBuilder.SuggestFileName(pedidos));
    }
}
