using BuscaTabelas.App.Services;

namespace BuscaTabelas.App.Views.Dialogs;

public sealed record MessageDialogModel(
    DialogKind Kind,
    string Title,
    string Message,
    string? Details,
    string ConfirmText,
    string? CancelText)
{
    public bool HasDetails => !string.IsNullOrWhiteSpace(Details);

    public bool HasCancel => !string.IsNullOrWhiteSpace(CancelText);

    public string Glyph => Kind switch
    {
        DialogKind.Error => "\uE711",
        DialogKind.Warning => "\uE7BA",
        _ => "\uE946"
    };
}
