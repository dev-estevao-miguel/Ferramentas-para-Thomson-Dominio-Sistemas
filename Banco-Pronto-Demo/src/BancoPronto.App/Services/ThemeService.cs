using System.Windows;

namespace BancoPronto.App.Services;

public interface IThemeService
{
    void Apply(bool dark);
}

/// <summary>
/// Troca o primeiro dicionário mesclado do App.xaml (o tema). Como as telas usam DynamicResource,
/// a mudança vale na hora, sem reabrir a janela.
/// </summary>
public sealed class ThemeService : IThemeService
{
    public void Apply(bool dark)
    {
        var theme = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/Resources/Themes/{(dark ? "Dark" : "Light")}.xaml", UriKind.Absolute)
        };

        Application.Current.Resources.MergedDictionaries[0] = theme;
    }
}
