using BuscaTabelas.App.ViewModels;
using BuscaTabelas.Core.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace BuscaTabelas.App.Services;

public interface INavigationService
{
    PageViewModel? CurrentPage { get; }

    event EventHandler? CurrentPageChanged;

    void NavigateTo<TPage>() where TPage : PageViewModel;

    void NavigateTo(Type pageType);
}

/// <summary>
/// Navegação por ViewModel. As páginas são singletons, então o estado (ex.: uma preparação em andamento)
/// é mantido ao trocar de menu. A View correspondente é escolhida por DataTemplate na MainWindow.
/// </summary>
public sealed class NavigationService : INavigationService
{
    private readonly IServiceProvider _services;
    private readonly ILoggingService _logger;

    public NavigationService(IServiceProvider services, ILoggingService logger)
    {
        _services = services;
        _logger = logger;
    }

    public PageViewModel? CurrentPage { get; private set; }

    public event EventHandler? CurrentPageChanged;

    public void NavigateTo<TPage>() where TPage : PageViewModel => NavigateTo(typeof(TPage));

    public void NavigateTo(Type pageType)
    {
        var page = (PageViewModel)_services.GetRequiredService(pageType);
        if (ReferenceEquals(page, CurrentPage))
        {
            return;
        }

        CurrentPage = page;
        CurrentPageChanged?.Invoke(this, EventArgs.Empty);
        NotifyNavigated(page);
    }

    private async void NotifyNavigated(PageViewModel page)
    {
        try
        {
            await page.OnNavigatedToAsync();
        }
        catch (Exception ex)
        {
            _logger.Error($"Falha ao carregar a página '{page.Title}'.", ex);
        }
    }
}
