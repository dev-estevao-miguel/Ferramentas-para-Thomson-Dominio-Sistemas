using System.Windows;
using BancoPronto.App.ViewModels;

namespace BancoPronto.App.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
