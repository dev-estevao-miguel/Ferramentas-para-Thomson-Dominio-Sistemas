using System.Windows;

namespace BuscaTabelas.App.Services;

/// <summary>Executa ações na thread da interface (para eventos que chegam de threads de background).</summary>
public interface IUiDispatcher
{
    void Post(Action action);
}

public sealed class UiDispatcher : IUiDispatcher
{
    public void Post(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.HasShutdownStarted)
        {
            return;
        }

        if (dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.BeginInvoke(action);
        }
    }
}
