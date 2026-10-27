using CommunityToolkit.Mvvm.ComponentModel;

namespace BuscaTabelas.App.ViewModels;

/// <summary>Base das páginas exibidas na área principal.</summary>
public abstract class PageViewModel : ObservableObject
{
    public abstract string Title { get; }

    /// <summary>Chamado sempre que a página se torna visível.</summary>
    public virtual Task OnNavigatedToAsync() => Task.CompletedTask;
}
