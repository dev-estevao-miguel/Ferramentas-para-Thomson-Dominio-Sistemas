using BuscaTabelas.Core.Catalog;

namespace BuscaTabelas.Core.Tests;

public sealed class CatalogSearchTests : IDisposable
{
    private readonly CatalogFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void Ignora_acentos_e_maiusculas_e_respeita_filtros_e_limite()
    {
        var catalog = _fixture.Load();

        var limited = catalog.Search(new CatalogQuery("SITUACAO", CatalogScope.Columns, CatalogSearchField.Description, Limit: 1));
        Assert.Single(limited.Hits);
        Assert.Equal(2, limited.Total);
        Assert.True(limited.IsTruncated);

        Assert.Equal(1, catalog.Search(new CatalogQuery("funcionarios", CatalogScope.Tables, CatalogSearchField.Description)).Total);
        Assert.Equal(0, catalog.Search(new CatalogQuery("vinculo", CatalogScope.Tables, CatalogSearchField.Description)).Total);
        Assert.Equal(1, catalog.Search(new CatalogQuery("vínculo", CatalogScope.Columns, CatalogSearchField.Description)).Total);
    }

    [Fact]
    public void Exige_todas_as_palavras()
    {
        var catalog = _fixture.Load();

        var result = catalog.Search(new CatalogQuery("demo ativo", CatalogScope.Columns));
        Assert.Equal(1, result.Total);
        Assert.Equal("demo.pessoas.sit\"uacao", result.Hits[0].FullName);
        Assert.Equal(0, catalog.Search(new CatalogQuery("demo inexistente")).Total);
    }

    [Fact]
    public void Sem_texto_retorna_tudo_na_ordem_do_arquivo()
    {
        var catalog = _fixture.Load();

        var result = catalog.Search(new CatalogQuery(""));
        Assert.Equal(6, result.Total);
        Assert.False(result.Hits[0].IsColumn);
        Assert.True(result.Hits[^1].IsColumn);
    }

    [Fact]
    public void Nome_exato_vem_antes_de_correspondencia_na_descricao()
    {
        var catalog = _fixture.Load();

        var result = catalog.Search(new CatalogQuery("situacao"));
        // "situacao" é exatamente o nome da tabela 3 e da coluna outro.pessoas.situacao.
        Assert.Equal("demo.situacao", result.Hits[0].FullName);
        Assert.Equal("outro.pessoas.situacao", result.Hits[1].FullName);
        Assert.Equal(3, result.Total);
    }

    [Theory]
    [InlineData("Situação", "situacao")]
    [InlineData("  AÇÃO  ", "  acao  ")]
    [InlineData(null, "")]
    public void Normaliza_texto(string? input, string expected) => Assert.Equal(expected, TextNormalizer.Normalize(input));
}
