using System.Diagnostics;
using System.Windows;

namespace BancoPronto.App.Services;

public interface IShellService
{
    void OpenFolder(string folder);

    void CopyToClipboard(string text);
}

public sealed class ShellService : IShellService
{
    public void OpenFolder(string folder) =>
        Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });

    public void CopyToClipboard(string text) => Clipboard.SetText(text);
}
