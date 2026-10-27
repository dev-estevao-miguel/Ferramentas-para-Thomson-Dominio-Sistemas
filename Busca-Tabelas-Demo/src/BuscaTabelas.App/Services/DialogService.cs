using System.Windows;
using BuscaTabelas.App.Views.Dialogs;
using Microsoft.Win32;

namespace BuscaTabelas.App.Services;

public enum DialogKind
{
    Info,
    Warning,
    Error
}

public interface IDialogService
{
    string? SelectFolder(string title, string? initialFolder = null);

    string? SelectFile(string title, string filter, string? initialFolder = null);

    /// <returns>Caminho escolhido para gravar, ou nulo se o usuário cancelar.</returns>
    string? SelectSaveFile(string title, string filter, string fileName, string defaultExtension);

    bool Confirm(string title, string message, string confirmText = "Confirmar", string cancelText = "Cancelar", DialogKind kind = DialogKind.Warning);

    void ShowError(string title, string message, string? technicalDetail = null);

    void ShowInfo(string title, string message);
}

public sealed class DialogService : IDialogService
{
    public string? SelectFolder(string title, string? initialFolder = null)
    {
        var dialog = new OpenFolderDialog { Title = title, Multiselect = false };
        if (!string.IsNullOrWhiteSpace(initialFolder) && System.IO.Directory.Exists(initialFolder))
        {
            dialog.InitialDirectory = initialFolder;
        }

        return dialog.ShowDialog(GetOwner()) == true ? dialog.FolderName : null;
    }

    public string? SelectFile(string title, string filter, string? initialFolder = null)
    {
        var dialog = new OpenFileDialog { Title = title, Filter = filter, CheckFileExists = true, Multiselect = false };
        if (!string.IsNullOrWhiteSpace(initialFolder) && System.IO.Directory.Exists(initialFolder))
        {
            dialog.InitialDirectory = initialFolder;
        }

        return dialog.ShowDialog(GetOwner()) == true ? dialog.FileName : null;
    }

    public string? SelectSaveFile(string title, string filter, string fileName, string defaultExtension)
    {
        var dialog = new SaveFileDialog
        {
            Title = title,
            Filter = filter,
            FileName = fileName,
            DefaultExt = defaultExtension,
            AddExtension = true,
            OverwritePrompt = true
        };

        return dialog.ShowDialog(GetOwner()) == true ? dialog.FileName : null;
    }

    public bool Confirm(string title, string message, string confirmText = "Confirmar", string cancelText = "Cancelar", DialogKind kind = DialogKind.Warning) =>
        Show(new MessageDialogModel(kind, title, message, null, confirmText, cancelText)) == true;

    public void ShowError(string title, string message, string? technicalDetail = null) =>
        Show(new MessageDialogModel(DialogKind.Error, title, message, technicalDetail, "Fechar", null));

    public void ShowInfo(string title, string message) =>
        Show(new MessageDialogModel(DialogKind.Info, title, message, null, "OK", null));

    private static bool? Show(MessageDialogModel model)
    {
        var dispatcher = Application.Current.Dispatcher;
        if (!dispatcher.CheckAccess())
        {
            return dispatcher.Invoke(() => Show(model));
        }

        var dialog = new MessageDialog(model);
        var owner = GetOwner();
        if (owner is not null)
        {
            dialog.Owner = owner;
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        return dialog.ShowDialog();
    }

    private static Window? GetOwner()
    {
        var main = Application.Current?.MainWindow;
        return main is { IsLoaded: true, IsVisible: true } ? main : null;
    }
}
