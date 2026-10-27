using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BuscaTabelas.App.Views;

public partial class SearchView : UserControl
{
    public SearchView()
    {
        InitializeComponent();
        // Ao abrir a página, o cursor já fica no campo de pesquisa.
        Loaded += (_, _) => Dispatcher.BeginInvoke(() => Keyboard.Focus(SearchBox), System.Windows.Threading.DispatcherPriority.Input);
        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is true)
            {
                Dispatcher.BeginInvoke(() => Keyboard.Focus(SearchBox), System.Windows.Threading.DispatcherPriority.Input);
            }
        };
    }
}
