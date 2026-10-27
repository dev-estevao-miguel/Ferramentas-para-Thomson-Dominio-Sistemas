# Identidade visual — B.T / Busca Tabelas

Símbolo próprio (TSCBr): tabela (cabeçalho escuro) com lupa, sobre o quadrado laranja #D64000 da família Banco Pronto.

| Arquivo | Onde aparece | Formato |
|---|---|---|
| `app.ico` | Ícone do `BuscaTabelas.exe`, da janela e da barra de tarefas | `.ico` com 16, 24, 32, 48, 64, 128 e 256 px |

- Na barra lateral e na tela Sobre o símbolo é desenhado em vetor por `Views/Controls/BrandLogo.xaml`, nítido em qualquer escala de tela.
- O `app.ico` é gerado do mesmo desenho: `powershell -NoProfile -STA -ExecutionPolicy Bypass -File tools\Gerar-Icone.ps1`. O SVG equivalente fica em `docs/marca/icone.svg`.
- Cores da marca ficam em `Resources/Themes/Light.xaml` e `Dark.xaml` (chaves `Brush.Accent*`).
