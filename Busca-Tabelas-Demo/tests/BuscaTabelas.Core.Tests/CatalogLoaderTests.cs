using System.Text.Json.Nodes;
using BuscaTabelas.Core.Catalog;

namespace BuscaTabelas.Core.Tests;

public sealed class CatalogLoaderTests : IDisposable
{
    private readonly CatalogFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void Carrega_metadados_tabelas_colunas_e_codigos()
    {
        var catalog = _fixture.Load(withBom: true);

        Assert.Equal("demo", catalog.Metadata.Server.Database);
        Assert.Equal("17.0 (demo)", catalog.Metadata.Server.Version);
        Assert.Equal(new DateTimeOffset(2026, 10, 14, 11, 18, 50, TimeSpan.FromHours(-3)), catalog.Metadata.GeneratedAt!.Value.AddTicks(-4240269));
        Assert.Equal(3, catalog.Tables.Count);
        Assert.Equal(3, catalog.ColumnCount);
        Assert.Equal(1, catalog.CodeCount);
        Assert.Equal(2, catalog.CountOfType("BASE"));

        var pessoas = catalog.FindTable(1)!;
        Assert.Equal(["sit\"uacao", "nome"], pessoas.Columns.Select(c => c.Name));
        var situacao = pessoas.Columns[0];
        Assert.True(situacao.IsPrimaryKey);
        Assert.False(situacao.AllowsNull);
        Assert.Equal(4, situacao.Size);
        Assert.Equal("1", situacao.DefaultValue);
        Assert.Equal([new CatalogCode("1", "Ativo")], situacao.Codes);
        Assert.Same(pessoas, situacao.Table);
        Assert.Empty(catalog.FindTable(2)!.Columns[0].Codes);
    }

    [Fact]
    public void Recusa_formato_desconhecido()
    {
        _fixture.Data["formato"] = "outro";
        var error = Assert.Throws<CatalogFormatException>(() => _fixture.Load());
        Assert.Contains("Formato de catálogo não reconhecido", error.Message);
    }

    [Fact]
    public void Recusa_json_invalido()
    {
        var path = Path.Combine(_fixture.Folder, "quebrado.json");
        File.WriteAllText(path, "{ \"formato\": ");
        Assert.Throws<CatalogFormatException>(() => new CatalogLoader().Load(path));
    }

    [Fact]
    public void Recusa_tabela_duplicada()
    {
        _fixture.Tables.Add(JsonNode.Parse("""{ "tabela_id": 9, "esquema": "demo", "tabela": "pessoas", "tipo": "BASE" }"""));
        var error = Assert.Throws<CatalogFormatException>(() => _fixture.Load());
        Assert.Contains("Tabela duplicada", error.Message);
    }

    [Fact]
    public void Recusa_coluna_orfa()
    {
        _fixture.Columns[0]!["tabela_id"] = 99;
        var error = Assert.Throws<CatalogFormatException>(() => _fixture.Load());
        Assert.Contains("Coluna sem tabela", error.Message);
    }

    [Fact]
    public void Recusa_codigo_sem_coluna()
    {
        _fixture.Codes[0]!["coluna"] = "inexistente";
        var error = Assert.Throws<CatalogFormatException>(() => _fixture.Load());
        Assert.Contains("Código sem coluna", error.Message);
    }

    [Fact]
    public void Recusa_id_de_coluna_repetido()
    {
        var copy = _fixture.Columns[1]!.DeepClone();
        copy["coluna"] = "outra";
        _fixture.Columns.Add(copy);
        var error = Assert.Throws<CatalogFormatException>(() => _fixture.Load());
        Assert.Contains("Identificador de coluna repetido", error.Message);
    }

    [Fact]
    public void Recusa_campo_obrigatorio_ausente()
    {
        _fixture.Tables[0]!.AsObject().Remove("tabela");
        var error = Assert.Throws<CatalogFormatException>(() => _fixture.Load());
        Assert.Contains("tabela", error.Message);
    }
}
