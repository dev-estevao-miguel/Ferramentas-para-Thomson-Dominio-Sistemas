using System.Runtime.InteropServices;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BuscaTabelas.App.Helpers;
using BuscaTabelas.App.Services;
using BuscaTabelas.Core.Logging;
using BuscaTabelas.Core.Services;

namespace BuscaTabelas.App.ViewModels;

/// <summary>
/// Sobre: produto, fluxo, informações do aplicativo e desenvolvimento.
/// "Copiar informações de diagnóstico" monta um resumo do ambiente, sem caminhos, conexões ou dados de clientes.
/// </summary>
public sealed partial class AboutViewModel : PageViewModel
{
    private const string LinkedInUrl = "https://www.linkedin.com/in/estev%C3%A3o-miguel-a22057171/";
    private const string GitHubUrl = "https://github.com/dev-estevao-miguel";

    // Tempo em que o botão mostra "Informações copiadas".
    private static readonly TimeSpan CopiedFeedbackDuration = TimeSpan.FromSeconds(2.2);

    private readonly IShellService _shell;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;
    private readonly ICatalogSession _catalog;
    private readonly ILoggingService _logger;
    private CancellationTokenSource? _copiedFeedback;

    [ObservableProperty]
    private bool _isDiagnosticsCopied;

    public AboutViewModel(
        IShellService shell,
        INavigationService navigation,
        IDialogService dialogs,
        ICatalogSession catalog,
        ILoggingService logger)
    {
        _shell = shell;
        _navigation = navigation;
        _dialogs = dialogs;
        _catalog = catalog;
        _logger = logger;
    }

    public override string Title => "Sobre";

    public string AppName => AppInfo.Name;

    public string Version => AppInfo.Version;

    /// <summary>"Windows x64".</summary>
    public string Platform => $"Windows {Architecture}";

    /// <summary>".NET 8".</summary>
    public string Runtime => $".NET {Environment.Version.Major}";

    public string Channel => "Demo";

    /// <summary>Os passos do fluxo antes de "Consulta pronta para uso" (que tem destaque próprio).</summary>
    public IReadOnlyList<string> FlowSteps { get; } =
    [
        "Conexão ODBC (opcional, desabilitada na demo)",
        "Leitura do catálogo do SQL Anywhere",
        "Leitura do dicionário de códigos",
        "Gravação do catálogo local (JSON)",
        "Validação e indexação",
        "Pesquisa por nome e descrição",
        "SELECT e contexto para IA"
    ];

    private static string Architecture => RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();

    [RelayCommand]
    private async Task CopyDiagnosticsAsync()
    {
        _shell.CopyToClipboard(BuildDiagnostics());
        _logger.Information("Informações de diagnóstico copiadas na tela Sobre.");

        _copiedFeedback?.Cancel();
        var feedback = _copiedFeedback = new CancellationTokenSource();
        IsDiagnosticsCopied = true;
        try
        {
            await Task.Delay(CopiedFeedbackDuration, feedback.Token);
            IsDiagnosticsCopied = false;
        }
        catch (TaskCanceledException)
        {
            // Copiado de novo antes do fim: o novo clique controla o retorno do botão.
        }
    }

    [RelayCommand]
    private void OpenLogs() => _navigation.NavigateTo<LogsViewModel>();

    [RelayCommand]
    private void OpenReleaseNotes() => _dialogs.ShowInfo(
        $"Notas da versão {Version}",
        "• Versão pública de demonstração, com catálogo fictício carregado na abertura.\n" +
        "• Pesquisa sem acentos e sem diferenciar maiúsculas, com os resultados mais prováveis primeiro.\n" +
        "• Tabela aberta com colunas, chave primária, códigos documentados, SELECT e contexto para IA.\n" +
        "• Exportação via ODBC disponível, mas desabilitada por padrão (Configurações).\n" +
        "• Tema claro, escuro ou do sistema.");

    [RelayCommand]
    private void OpenLicense() => _dialogs.ShowInfo(
        "Licença",
        "Busca Tabelas Demo © 2026 TSCBr. Licença ainda não definida.\n\n" +
        "Componentes de terceiros:\n" +
        "• Source Sans 3: SIL Open Font License 1.1\n" +
        "• CommunityToolkit.Mvvm e Microsoft.Extensions.DependencyInjection: licença MIT\n\n" +
        "Thomson Reuters, Domínio Sistemas, SQL Anywhere e demais marcas mencionadas pertencem aos seus respectivos proprietários. " +
        "Este projeto não é um produto oficial da Thomson Reuters.");

    [RelayCommand]
    private void OpenPrivacy() => _dialogs.ShowInfo(
        "Privacidade",
        "O Busca Tabelas trabalha nesta máquina e não envia nada para a internet.\n\n" +
        "A demonstração usa somente um catálogo fictício. A exportação via ODBC vem desabilitada e, quando habilitada, " +
        "lê apenas metadados do banco (tabelas, colunas e descrições), nunca registros.\n\n" +
        "A senha é digitada na janela do PowerShell: não passa pelo B.T, não é gravada e não vai para os logs.\n\n" +
        "O contexto para IA é salvo em arquivo local; revise o conteúdo antes de compartilhá-lo.");

    [RelayCommand]
    private void OpenLinkedIn() => _shell.OpenUrl(LinkedInUrl);

    [RelayCommand]
    private void OpenGitHub() => _shell.OpenUrl(GitHubUrl);

    private string BuildDiagnostics()
    {
        var catalog = _catalog.Current;
        var catalogText = catalog is null
            ? "nenhum carregado"
            : $"{Formats.DateTime(catalog.Metadata.GeneratedAt)}, SQL Anywhere {catalog.Metadata.Server.Version ?? "?"}, " +
              $"{catalog.Tables.Count} tabelas/views, {catalog.ColumnCount} colunas, {catalog.CodeCount} códigos";

        return new StringBuilder()
            .AppendLine($"Busca Tabelas Demo: {Version}")
            .AppendLine($"Windows: {Environment.OSVersion.VersionString} {Architecture}")
            .AppendLine($".NET: {RuntimeInformation.FrameworkDescription}")
            .AppendLine($"Catálogo: {catalogText}")
            .Append($"Data/hora: {DateTime.Now:dd/MM/yyyy HH:mm:ss}")
            .ToString();
    }
}
