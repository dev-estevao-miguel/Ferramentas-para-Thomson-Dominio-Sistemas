using System.Threading;
using System.Windows;
using BuscaTabelas.App.Helpers;
using BuscaTabelas.App.Services;
using BuscaTabelas.App.ViewModels;
using BuscaTabelas.App.Views;
using BuscaTabelas.Core.Configuration;
using BuscaTabelas.Core.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace BuscaTabelas.App;

public partial class App : Application
{
    // Uma única instância por usuário: abrir o B.T de novo apenas traz a janela existente.
    private const string InstanceMutexName = @"Local\BuscaTabelasDemo.Instancia";
    private const string ActivateEventName = @"Local\BuscaTabelasDemo.Ativar";

    private ServiceProvider? _services;
    private ILoggingService? _logger;
    private Mutex? _instanceMutex;
    private EventWaitHandle? _activateEvent;
    private RegisteredWaitHandle? _activateRegistration;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (!AcquireSingleInstance())
        {
            Shutdown();
            return;
        }

        _logger = new FileLoggingService(AppPaths.LogsFolder);
        RegisterGlobalExceptionHandlers(_logger);

        try
        {
            _services = ServiceRegistration.Build(_logger);

            var configuration = _services.GetRequiredService<IConfigurationService>();
            _services.GetRequiredService<IThemeService>().Apply(configuration.Current.Theme);
            _logger.Information($"{AppInfo.Name} {AppInfo.Version} iniciado.");

            var window = _services.GetRequiredService<MainWindow>();
            MainWindow = window;
            window.Show();

            await _services.GetRequiredService<MainViewModel>().InitializeAsync(e.Args.FirstOrDefault());
        }
        catch (Exception ex)
        {
            _logger.Error("Falha ao iniciar o aplicativo.", ex);
            MessageBox.Show(
                $"Não foi possível iniciar o aplicativo.\n\n{ex.Message}",
                AppInfo.Name,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _logger?.Information("Aplicativo encerrado.");
        _activateRegistration?.Unregister(null);
        _activateEvent?.Dispose();
        _services?.Dispose();
        if (_instanceMutex is not null)
        {
            _instanceMutex.ReleaseMutex();
            _instanceMutex.Dispose();
        }

        base.OnExit(e);
    }

    /// <returns>Falso quando já existe um B.T aberto (ele é avisado para mostrar a janela).</returns>
    private bool AcquireSingleInstance()
    {
        _instanceMutex = new Mutex(initiallyOwned: true, InstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            _instanceMutex.Dispose();
            _instanceMutex = null;
            try
            {
                using var existing = EventWaitHandle.OpenExisting(ActivateEventName);
                existing.Set();
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                // A outra instância ainda está iniciando; ela mesma exibirá a janela.
            }

            return false;
        }

        _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
        _activateRegistration = ThreadPool.RegisterWaitForSingleObject(
            _activateEvent,
            (_, _) => Dispatcher.BeginInvoke(() => (MainWindow as MainWindow)?.BringToFront()),
            null,
            Timeout.Infinite,
            executeOnlyOnce: false);
        return true;
    }

    /// <summary>Nenhum erro deve fechar o aplicativo silenciosamente: tudo é registrado e, na interface, exibido de forma amigável.</summary>
    private void RegisterGlobalExceptionHandlers(ILoggingService logger)
    {
        DispatcherUnhandledException += (_, args) =>
        {
            logger.Error("Erro não tratado na interface.", args.Exception);
            args.Handled = true;
            ShowUnexpectedError(args.Exception);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            logger.Error("Erro fatal não tratado.", args.ExceptionObject as Exception);

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            logger.Error("Erro não observado em tarefa de segundo plano.", args.Exception);
            args.SetObserved();
        };
    }

    private void ShowUnexpectedError(Exception exception)
    {
        try
        {
            var dialogs = _services?.GetService<IDialogService>();
            if (dialogs is not null)
            {
                dialogs.ShowError(
                    "Algo não saiu como esperado",
                    "Ocorreu um erro inesperado. O aplicativo continua funcionando e o problema foi registrado no log.",
                    $"{exception.GetType().Name}: {exception.Message}");
                return;
            }
        }
        catch (Exception dialogError)
        {
            // Último recurso: se nem o diálogo puder ser exibido, cai para a MessageBox nativa.
            _logger?.Warning("Falha ao exibir o diálogo de erro.", dialogError);
        }

        MessageBox.Show("Ocorreu um erro inesperado. Detalhes foram registrados no log.", AppInfo.Name,
            MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
