using System.Windows;
using System.Windows.Input;

namespace BuscaTabelas.App.Views.Dialogs;

/// <summary>Diálogo padrão do aplicativo. O code-behind trata apenas comportamento de janela.</summary>
public partial class MessageDialog : Window
{
    public MessageDialog(MessageDialogModel model)
    {
        InitializeComponent();
        DataContext = model;
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnDragAreaMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
