using System.Diagnostics;
using System.Text;
using BuscaTabelas.Core.Catalog;
using BuscaTabelas.Core.Configuration;
using BuscaTabelas.Core.Logging;

namespace BuscaTabelas.Core.Collector;

public enum CollectorMode
{
    /// <summary>Conecta e conta as tabelas visíveis, sem gravar nada.</summary>
    Testar,

    /// <summary>Exporta tabelas, colunas e códigos para um novo catalogo.json.</summary>
    Exportar
}

public sealed record CollectorRequest(
    CollectorMode Mode,
    string Dsn,
    string User,
    string Schema,
    int TimeoutSeconds,
    string OutputFolder);

/// <param name="NewCatalog">Exportação criada por esta execução (somente no modo Exportar, com sucesso).</param>
public sealed record CollectorResult(int ExitCode, CatalogFileInfo? NewCatalog)
{
    public bool Succeeded => ExitCode == 0;
}

public interface ICollectorService
{
    /// <summary>PowerShell de 32 bits (os DSNs do SQL Anywhere encontrados são de 32 bits).</summary>
    string PowerShellPath { get; }

    /// <summary>
    /// Abre o coletor em uma janela do PowerShell e aguarda o usuário fechá-la. A senha é digitada
    /// nessa janela: não passa pelo aplicativo, não é gravada nem vai para os logs.
    /// </summary>
    Task<CollectorResult> RunAsync(CollectorRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Executa o coletor (tools/coletor/BT.ps1, embutido no executável). Ele só faz SELECTs fixos de metadados:
/// não consulta registros de empresas, funcionários ou lançamentos.
/// </summary>
public sealed class CollectorService : ICollectorService
{
    private const string ResourcePrefix = "BuscaTabelas.Coletor.";

    private readonly ILoggingService _logger;

    public CollectorService(ILoggingService logger)
    {
        _logger = logger;
    }

    public string PowerShellPath { get; } = LocatePowerShell32();

    public async Task<CollectorResult> RunAsync(CollectorRequest request, CancellationToken cancellationToken = default)
    {
        var script = ExtractScripts(AppPaths.CollectorFolder);
        var before = CatalogLocator.List(request.OutputFolder).Select(c => c.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var startInfo = new ProcessStartInfo(PowerShellPath)
        {
            // Sem shell e sem ocultar: o console é criado para o PowerShell, em uma janela própria.
            UseShellExecute = false,
            CreateNoWindow = false,
            WorkingDirectory = AppPaths.CollectorFolder,
            ArgumentList =
            {
                "-NoProfile",
                "-ExecutionPolicy", "Bypass",
                "-EncodedCommand", CollectorCommand.Encode(CollectorCommand.Build(request, script))
            }
        };

        _logger.Information($"Coletor iniciado ({request.Mode}): conexão '{request.Dsn}', usuário '{request.User}', esquema '{request.Schema}'.");
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Não foi possível abrir o PowerShell.");
        await process.WaitForExitAsync(cancellationToken);

        CatalogFileInfo? created = null;
        if (process.ExitCode == 0 && request.Mode == CollectorMode.Exportar)
        {
            created = CatalogLocator.List(request.OutputFolder).FirstOrDefault(c => !before.Contains(c.Path));
        }

        _logger.Information($"Coletor encerrado com código {process.ExitCode}." + (created is null ? string.Empty : $" Novo catálogo: {created.Path}"));
        return new CollectorResult(process.ExitCode, created);
    }

    /// <summary>Grava BT.ps1 e sql\*.sql na pasta do coletor e devolve o caminho do script.</summary>
    private static string ExtractScripts(string folder)
    {
        var assembly = typeof(CollectorService).Assembly;
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal)))
        {
            // "BuscaTabelas.Coletor.sql.tabelas.sql" → "sql\tabelas.sql"; "BuscaTabelas.Coletor.BT.ps1" → "BT.ps1"
            var relative = name[ResourcePrefix.Length..];
            if (relative.StartsWith("sql.", StringComparison.Ordinal))
            {
                relative = Path.Combine("sql", relative["sql.".Length..]);
            }

            var target = Path.Combine(folder, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using var resource = assembly.GetManifestResourceStream(name)!;
            using var file = File.Create(target);
            resource.CopyTo(file);
        }

        return Path.Combine(folder, "BT.ps1");
    }

    private static string LocatePowerShell32()
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var wow64 = Path.Combine(windows, "SysWOW64", "WindowsPowerShell", "v1.0", "powershell.exe");
        return File.Exists(wow64) ? wow64 : Path.Combine(windows, "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
    }
}

/// <summary>Comando entregue ao PowerShell: roda o BT.ps1, mostra o resultado e espera o Enter para fechar.</summary>
public static class CollectorCommand
{
    public static string Build(CollectorRequest request, string scriptPath)
    {
        var mode = request.Mode == CollectorMode.Exportar ? "Exportar" : "Testar";
        var timeout = Math.Clamp(request.TimeoutSeconds, 1, 300);

        // Mensagens sem acentos, como o BT.ps1: o console do Windows PowerShell nem sempre usa UTF-8.
        return new StringBuilder()
            .AppendLine($"$Host.UI.RawUI.WindowTitle = {Quote($"B.T - Coletor ({mode}) - {request.Dsn}")}")
            .AppendLine("$global:LASTEXITCODE = 0")
            .AppendLine("$code = 0")
            .AppendLine("try {")
            .AppendLine($"    & {Quote(scriptPath)} -Modo {mode} -Dsn {Quote(request.Dsn)} -Usuario {Quote(request.User)} " +
                        $"-Esquema {Quote(request.Schema)} -Timeout {timeout} -Saida {Quote(request.OutputFolder)}")
            .AppendLine("    if ($LASTEXITCODE) { $code = $LASTEXITCODE }")
            .AppendLine("} catch {")
            .AppendLine("    Write-Host ''")
            .AppendLine("    Write-Host $_.Exception.Message -ForegroundColor Red")
            .AppendLine("    $code = 1")
            .AppendLine("}")
            .AppendLine("Write-Host ''")
            .AppendLine("if ($code -eq 0) { Write-Host 'Concluido. Feche esta janela para voltar ao B.T.' -ForegroundColor Green }")
            .AppendLine("else { Write-Host 'Nao concluido. Confira a mensagem acima.' -ForegroundColor Yellow }")
            .AppendLine("Read-Host 'Pressione Enter para fechar' | Out-Null")
            .AppendLine("exit $code")
            .ToString();
    }

    /// <summary>-EncodedCommand: Base64 de UTF-16LE. Evita qualquer problema de aspas e acentos na linha de comando.</summary>
    public static string Encode(string command) => Convert.ToBase64String(Encoding.Unicode.GetBytes(command));

    /// <summary>
    /// Texto literal do PowerShell entre aspas simples. Aspas simples internas são duplicadas, inclusive as
    /// tipográficas (‘ ’ ‚ ‛), que o PowerShell também aceita como delimitador.
    /// </summary>
    public static string Quote(string value)
    {
        var builder = new StringBuilder(value.Length + 2).Append('\'');
        foreach (var c in value)
        {
            builder.Append(c);
            if (c is '\'' or '‘' or '’' or '‚' or '‛')
            {
                builder.Append(c);
            }
        }

        return builder.Append('\'').ToString();
    }
}
