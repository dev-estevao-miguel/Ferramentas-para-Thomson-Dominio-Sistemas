using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace BancoPronto.App.Services;

public interface IFolderPicker
{
    /// <returns>Pasta escolhida, ou nulo se o usuário cancelar.</returns>
    string? PickFolder(string? initialFolder);
}

/// <summary>Seletor de pastas nativo do Windows (OpenFolderDialog, .NET 8).</summary>
public sealed class FolderPicker : IFolderPicker
{
    public string? PickFolder(string? initialFolder)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Pasta dos backups",
            Multiselect = false
        };

        if (!string.IsNullOrWhiteSpace(initialFolder) && Directory.Exists(initialFolder))
        {
            dialog.InitialDirectory = initialFolder;
        }

        return dialog.ShowDialog(Application.Current.MainWindow) == true ? dialog.FolderName : null;
    }
}
