using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BuscaTabelas.Core.Catalog;

namespace BuscaTabelas.Core.Tests;

/// <summary>Catálogo mínimo no formato do coletor, gravado numa pasta temporária.</summary>
public sealed class CatalogFixture : IDisposable
{
    public CatalogFixture()
    {
        Folder = Path.Combine(Path.GetTempPath(), "bt-testes-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Folder);
        Data = JsonNode.Parse("""
            {
              "formato": "bt-catalogo-v1",
              "gerado_em": "2026-10-14T11:18:50.4240269-03:00",
              "servidor": [{ "versao_servidor": "17.0 (demo)", "banco": "demo" }],
              "esquema": "DEMO",
              "status_valores": "disponivel",
              "avisos": [],
              "tabelas": [
                { "tabela_id": 1, "esquema": "demo", "tabela": "pessoas", "tipo": "BASE", "descricao": "Funcionários" },
                { "tabela_id": 2, "esquema": "outro", "tabela": "pessoas", "tipo": "VIEW", "descricao": "" },
                { "tabela_id": 3, "esquema": "demo", "tabela": "situacao", "tipo": "BASE", "descricao": "Situações cadastrais" }
              ],
              "colunas": [
                { "tabela_id": 1, "coluna_id": 2, "esquema": "demo", "tabela": "pessoas", "coluna": "nome", "tipo": "varchar",
                  "tamanho_ou_precisao": 60, "escala": 0, "chave_primaria": "N", "aceita_nulo": "Y", "valor_padrao": null, "descricao": "Nome" },
                { "tabela_id": 1, "coluna_id": 1, "esquema": "demo", "tabela": "pessoas", "coluna": "sit\"uacao", "tipo": "integer",
                  "tamanho_ou_precisao": 4, "escala": 0, "chave_primaria": "Y", "aceita_nulo": "N", "valor_padrao": "1",
                  "descricao": "Situação | vínculo\nativo" },
                { "tabela_id": 2, "coluna_id": 1, "esquema": "outro", "tabela": "pessoas", "coluna": "situacao", "tipo": "integer", "descricao": "Situação" }
              ],
              "valores": [
                { "esquema": "demo", "tabela": "pessoas", "coluna": "sit\"uacao", "valor": "1", "descricao": "Ativo" }
              ]
            }
            """)!.AsObject();
    }

    public string Folder { get; }

    public JsonObject Data { get; }

    public JsonArray Tables => Data["tabelas"]!.AsArray();

    public JsonArray Columns => Data["colunas"]!.AsArray();

    public JsonArray Codes => Data["valores"]!.AsArray();

    public string Write(string fileName = "catalogo.json", bool withBom = false)
    {
        var path = Path.Combine(Folder, fileName);
        File.WriteAllText(path, Data.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(withBom));
        return path;
    }

    public DatabaseCatalog Load(bool withBom = false) => new CatalogLoader().Load(Write(withBom: withBom));

    public void Dispose()
    {
        try
        {
            Directory.Delete(Folder, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
