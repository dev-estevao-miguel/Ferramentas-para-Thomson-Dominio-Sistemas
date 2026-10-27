using BancoPronto.App.Services;
using BancoPronto.App.ViewModels;
using BancoPronto.App.Views;
using BancoPronto.Core.Abstractions;
using BancoPronto.Core.Demo;
using BancoPronto.Core.Preparation;
using BancoPronto.Core.Scanning;
using Microsoft.Extensions.DependencyInjection;

namespace BancoPronto.App;

/// <summary>
/// Composição dos serviços. Cada etapa do fluxo é uma interface: para trocar a simulação por outra
/// implementação basta registrá-la aqui, sem mexer no orquestrador nem nas telas.
/// </summary>
public static class ServiceRegistration
{
    public static ServiceProvider Build()
    {
        var services = new ServiceCollection();

        // Core: verificação e preparação (implementações de demonstração)
        services.AddSingleton(new DemoOptions());
        services.AddSingleton<IBackupScanner, BackupScanner>();
        services.AddSingleton<IBackupExtractor, DemoBackupExtractor>();
        services.AddSingleton<ILogProcessor, DemoLogProcessor>();
        services.AddSingleton<IValidationService, DemoValidationService>();
        services.AddSingleton<IPreparationService, PreparationService>();

        // Interface
        services.AddSingleton<IFolderPicker, FolderPicker>();
        services.AddSingleton<IShellService, ShellService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
    }
}
