using BuscaTabelas.App.Services;
using BuscaTabelas.App.ViewModels;
using BuscaTabelas.App.Views;
using BuscaTabelas.Core.Catalog;
using BuscaTabelas.Core.Collector;
using BuscaTabelas.Core.Configuration;
using BuscaTabelas.Core.Logging;
using BuscaTabelas.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BuscaTabelas.App;

/// <summary>
/// Composição dos serviços. Para evoluir uma parte basta trocar a implementação registrada aqui, sem mexer nas telas.
/// </summary>
public static class ServiceRegistration
{
    public static ServiceProvider Build(ILoggingService logger)
    {
        var services = new ServiceCollection();

        // Core: infraestrutura
        services.AddSingleton(logger);
        services.AddSingleton<IConfigurationService>(sp =>
            new JsonConfigurationService(AppPaths.SettingsFile, sp.GetRequiredService<ILoggingService>()));

        // Core: catálogo e coletor
        services.AddSingleton<ICatalogLoader, CatalogLoader>();
        services.AddSingleton<ICatalogSession, CatalogSession>();
        services.AddSingleton<ICollectorService, CollectorService>();

        // Interface
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IShellService, ShellService>();
        services.AddSingleton<IUiDispatcher, UiDispatcher>();
        services.AddSingleton<ICatalogOpener, CatalogOpener>();

        // ViewModels (singletons: o estado da página é mantido ao navegar)
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<HomeViewModel>();
        services.AddSingleton<SearchViewModel>();
        services.AddSingleton<CollectorViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<LogsViewModel>();
        services.AddSingleton<AboutViewModel>();

        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
    }
}
