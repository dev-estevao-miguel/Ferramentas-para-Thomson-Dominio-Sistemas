using System.Diagnostics;
using System.IO;
using System.Windows;
using BuscaTabelas.Core.Logging;

namespace BuscaTabelas.App.Services;

/// <summary>Integrações com o Windows: abrir pastas, links e área de transferência.</summary>
public interface IShellService
{
    void OpenFolder(string path);

    /// <summary>Abre o Explorer com o arquivo selecionado.</summary>
    void RevealFile(string path);

    void OpenUrl(string url);

    /// <returns>Falso quando a área de transferência estava ocupada por outro programa.</returns>
    bool CopyToClipboard(string text);
}

public sealed class ShellService : IShellService
{
    private readonly ILoggingService _logger;

    public ShellService(ILoggingService logger)
    {
        _logger = logger;
    }

    public void OpenFolder(string path)
    {
        if (!Directory.Exists(path))
        {
            _logger.Warning($"Pasta não encontrada para abrir: '{path}'.");
            return;
        }

        Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { path }, UseShellExecute = false });
    }

    public void RevealFile(string path)
    {
        if (!File.Exists(path))
        {
            OpenFolder(Path.GetDirectoryName(path) ?? path);
            return;
        }

        Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { "/select,", path }, UseShellExecute = false });
    }

    public void OpenUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            _logger.Warning("Link ignorado: somente endereços da web são abertos.");
            return;
        }

        Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }

    public bool CopyToClipboard(string text)
    {
        try
        {
            Clipboard.SetText(text);
            return true;
        }
        catch (System.Runtime.InteropServices.ExternalException ex)
        {
            _logger.Warning("Não foi possível copiar para a área de transferência.", ex);
            return false;
        }
    }

    private void Start(ProcessStartInfo startInfo)
    {
        try
        {
            using var _ = Process.Start(startInfo);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _logger.Warning($"Não foi possível abrir '{startInfo.FileName}'.", ex);
        }
    }
}
