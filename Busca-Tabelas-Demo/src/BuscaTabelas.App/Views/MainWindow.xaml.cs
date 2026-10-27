using System.ComponentModel;
using System.IO;
using System.Windows;
using BuscaTabelas.App.Services;
using BuscaTabelas.App.ViewModels;
using BuscaTabelas.Core.Configuration;

namespace BuscaTabelas.App.Views;

public partial class MainWindow : Window
{
    private const double DefaultWidth = 1280;
    private const double DefaultHeight = 820;

    private readonly IConfigurationService _configuration;

    public MainWindow(MainViewModel viewModel, IConfigurationService configuration, IThemeService theme)
    {
        InitializeComponent();
        SourceInitialized += (_, _) => theme.ApplyTitleBar(this);
        DataContext = viewModel;
        _configuration = configuration;
        ApplyInitialSize();
    }

    /// <summary>Traz a janela para a frente (nova abertura do B.T com ele já aberto).</summary>
    public void BringToFront()
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
        // Garante que a janela venha para a frente mesmo se outra estiver em foco.
        Topmost = true;
        Topmost = false;
        Focus();
    }

    /// <summary>Usa o último tamanho salvo, sempre limitado à área útil da tela (sem cobrir a barra de tarefas).</summary>
    private void ApplyInitialSize()
    {
        var workArea = SystemParameters.WorkArea;
        var settings = _configuration.Current;

        MinWidth = Math.Min(MinWidth, workArea.Width);
        MinHeight = Math.Min(MinHeight, workArea.Height);
        Width = Math.Clamp(settings.WindowWidth ?? DefaultWidth, MinWidth, workArea.Width);
        Height = Math.Clamp(settings.WindowHeight ?? DefaultHeight, MinHeight, workArea.Height);

        if (settings.WindowMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        var size = WindowState == WindowState.Normal ? new Size(ActualWidth, ActualHeight) : RestoreBounds.Size;
        if (size.IsEmpty || size.Width <= 0 || size.Height <= 0)
        {
            return;
        }

        try
        {
            _configuration.Update(settings =>
            {
                settings.WindowWidth = Math.Round(size.Width);
                settings.WindowHeight = Math.Round(size.Height);
                settings.WindowMaximized = WindowState == WindowState.Maximized;
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Não salvar o tamanho da janela nunca deve impedir o fechamento.
        }
    }
}
