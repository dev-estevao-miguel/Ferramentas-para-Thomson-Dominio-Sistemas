using System.Windows;
using System.Windows.Threading;
using BancoPronto.App.Views;
using Microsoft.Extensions.DependencyInjection;

namespace BancoPronto.App;

public partial class App : Application
{
    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandledException;

        _services = ServiceRegistration.Build();
        var window = _services.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }

    /// <summary>Nenhum erro fecha o aplicativo em silêncio: a mensagem é exibida e a janela continua aberta.</summary>
    private static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"Ocorreu um erro inesperado. O aplicativo continua funcionando.\n\n{e.Exception.Message}",
            "Banco Pronto Demo",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        e.Handled = true;
    }
}
