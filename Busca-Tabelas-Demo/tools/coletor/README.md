# Coletor do B.T

> Na demonstração, o coletor vem **desabilitado** no aplicativo (Configurações › Coletor ODBC). O B.T funciona apenas com o catálogo fictício de `samples/`.

`BT.ps1` lê os metadados de um banco SQL Anywhere via ODBC e grava o `catalogo.json` (formato `bt-catalogo-v1`) que o aplicativo pesquisa. Executa apenas os SELECTs fixos de `sql/`: não aceita SQL do usuário e não consulta registros das tabelas.

Esta pasta é a **única fonte** do coletor. O projeto `BuscaTabelas.Core` embute `BT.ps1` e `sql/*.sql` no executável; o aplicativo os extrai para `%LocalAppData%\BuscaTabelasDemo\Coletor` e roda pela tela **Atualizar catálogo**.

Requisitos: Windows PowerShell 5.1 e o driver ODBC do SQL Anywhere. O coletor roda no PowerShell de `SysWOW64` porque DSNs de 32 bits só são visíveis para processos de 32 bits.

## Modos

```powershell
$ps32 = "$env:WINDIR\SysWOW64\WindowsPowerShell\v1.0\powershell.exe"

# Drivers e conexões do SQL Anywhere, sem conectar
& $ps32 -NoProfile -ExecutionPolicy Bypass -File .\tools\coletor\BT.ps1 -Modo Diagnostico

# Conecta e conta as tabelas visíveis, sem gravar nada
& $ps32 -NoProfile -ExecutionPolicy Bypass -File .\tools\coletor\BT.ps1 -Modo Testar -Dsn 'DSN_EXEMPLO' -Usuario 'USUARIO_EXEMPLO'

# Exporta tabelas e colunas
& $ps32 -NoProfile -ExecutionPolicy Bypass -File .\tools\coletor\BT.ps1 -Modo Exportar -Dsn 'DSN_EXEMPLO' -Usuario 'USUARIO_EXEMPLO' -Timeout 120
```

`-ExecutionPolicy Bypass` vale só para esse processo; não altera a política da máquina. A senha é sempre pedida no terminal e não é gravada. Sem `-Usuario`, o usuário também é pedido.

| Parâmetro | Padrão | Uso |
|---|---|---|
| `-Dsn` | — | Conexão ODBC (obrigatória para Testar/Exportar) |
| `-Usuario` | pergunta | Usuário com permissão de leitura do catálogo |
| `-Esquema` | `DBA` | Proprietário das tabelas a exportar |
| `-Timeout` | `30` | Segundos por comando (1 a 300) |
| `-Saida` | `%LocalAppData%\BuscaTabelasDemo\Catalogos` | Mesma pasta lida pelo aplicativo |

Cada exportação cria `<Saida>\<aaaaMMdd-HHmmss-id>\catalogo.json` com tabelas, colunas, descrições, tipos, tamanho/precisão, escala, chave primária, nulos e valores padrão. Não contém registros, credenciais, índices nem chaves estrangeiras.

## Dicionário de códigos (opcional)

Códigos documentados (ex.: `1 = Ativo`) dependem do dicionário de dados de cada produto, por isso não há consulta padrão. Para incluí-los, crie `sql/valores.sql` retornando as colunas `esquema`, `tabela`, `coluna`, `valor` e `descricao`, filtradas pelo esquema recebido como parâmetro (`?`). Sem esse arquivo, o catálogo é salvo com `status_valores: indisponivel`.

## Falhas

O coletor mostra códigos ODBC (`SQLState/erro nativo`) sem imprimir a mensagem bruta da conexão, para não expor parâmetros de acesso.

- `28000/-103`: usuário/senha rejeitados.
- DSN não encontrado: confira o nome e a arquitetura (32 bits).
- Catálogo vazio: confira banco, esquema e permissões.

Ao alterar `BT.ps1` ou `sql/`, recompile o aplicativo para atualizar a cópia embutida.
