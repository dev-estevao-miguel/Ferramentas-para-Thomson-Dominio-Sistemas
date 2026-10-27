# Como usar o B.T (demo)

## Catálogo de demonstração

Na primeira execução, o B.T abre `samples/catalogo-demo.json`: um catálogo **fictício** (esquema `demo`) com clientes, pedidos, produtos, notas fiscais e funcionários de uma empresa imaginária. Nenhuma conexão com banco é necessária.

Para voltar a ele a qualquer momento, use **Abrir catálogo de demonstração** (Início ou Atualizar catálogo), ou **Abrir arquivo…** para escolher outro `catalogo.json`.

## Pesquisar

Digite parte do nome ou da descrição: `cliente`, `pedido`, `nota fiscal`, `salario`. A busca ignora maiúsculas e acentos; com várias palavras, todas precisam aparecer. Use **Todos / Tabelas / Colunas** e **Nome e descrição / Nome / Descrição** para filtrar. Os resultados mais prováveis vêm primeiro (nome exato, depois nomes que começam com o termo). Esc limpa a pesquisa.

A lista mostra até 300 resultados (ajustável em Configurações), com o total encontrado ao lado dos filtros.

## Tabela, colunas e códigos

Clique num resultado para abrir a tabela. Se o resultado for uma coluna, ela já fica marcada e visível.

- **PK** (chave) indica chave primária; **Nulo** indica se o campo aceita nulos. Tamanho/precisão e escala são metadados do tipo e não significam a mesma coisa para todos os tipos.
- **Campo e códigos** mostra descrição, tipo, valor padrão e os códigos documentados da última coluna marcada (ex.: `PEDIDOS_DEMO.SITUACAO`).
- A ausência de códigos no catálogo não significa ausência de validações no sistema. Relações entre tabelas e regras de negócio não são extraídas.

## SELECT

Marque uma ou mais colunas (Ctrl/Shift + clique, ou **Todas**). A aba **SELECT** mostra a consulta e **Copiar SELECT** a coloca na área de transferência. O texto inclui esquema e nomes entre aspas. O B.T não executa consultas.

## Contexto para IA

**Exportar para IA…** salva um Markdown com todas as colunas da tabela, descrições, tipos, códigos e dados da extração. É um recorte bem menor que o catálogo inteiro, adequado para acompanhar um pedido de revisão de SQL. Nada é enviado automaticamente; revise o conteúdo antes de compartilhar.

## Exportar o catálogo de um banco próprio (opcional)

O coletor ODBC vem **desabilitado** na demonstração. Para usá-lo com um banco SQL Anywhere seu:

1. **Configurações › Coletor ODBC**: marque **Habilitar exportação via ODBC** e salve.
2. Em **Atualizar catálogo**, informe a conexão (DSN de 32 bits), o usuário e o esquema.
3. Clique em **Exportar catálogo** e digite a senha na janela do PowerShell. Ela não é gravada nem passa pelo B.T.

Cada exportação vai para uma subpasta nova em `%LocalAppData%\BuscaTabelasDemo\Catalogos`. Detalhes em [tools/coletor](../tools/coletor/README.md).
