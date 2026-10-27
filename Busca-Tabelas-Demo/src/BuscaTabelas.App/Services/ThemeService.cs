using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using BuscaTabelas.Core.Configuration;
using BuscaTabelas.Core.Logging;
using Microsoft.Win32;

namespace BuscaTabelas.App.Services;

public interface IThemeService
{
    AppTheme CurrentTheme { get; }

    void Apply(AppTheme theme);

    /// <summary>Barra de título do Windows escura no tema escuro (chamar quando a janela tiver handle).</summary>
    void ApplyTitleBar(Window window);
}

/// <summary>
/// Troca o dicionário de cores (Resources/Themes/*.xaml) em tempo de execução.
/// No modo "Sistema", acompanha a preferência de aplicativos claro/escuro do Windows.
/// </summary>
public sealed class ThemeService : IThemeService
{
    private const string ThemesFolder = "/Resources/Themes/";
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    // DWMWA_USE_IMMERSIVE_DARK_MODE: 20 no Windows 10 20H1+ e 11; 19 em builds anteriores.
    private const int DarkModeAttribute = 20;
    private const int DarkModeAttributeLegacy = 19;

    private readonly ILoggingService _logger;
    private bool _isDark;

    public ThemeService(ILoggingService logger)
    {
        _logger = logger;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public AppTheme CurrentTheme { get; private set; } = AppTheme.Light;

    public void Apply(AppTheme theme)
    {
        CurrentTheme = theme;
        var effective = theme == AppTheme.System ? (IsWindowsInDarkMode() ? AppTheme.Dark : AppTheme.Light) : theme;
        _isDark = effective == AppTheme.Dark;
        foreach (Window window in Application.Current.Windows)
        {
            ApplyTitleBar(window);
        }

        var source = new Uri($"{ThemesFolder}{(effective == AppTheme.Dark ? "Dark" : "Light")}.xaml", UriKind.Relative);

        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var current = dictionaries.FirstOrDefault(d =>
            d.Source?.OriginalString.Contains(ThemesFolder, StringComparison.OrdinalIgnoreCase) == true);

        if (current?.Source == source)
        {
            return;
        }

        var replacement = new ResourceDictionary { Source = source };
        if (current is null)
        {
            dictionaries.Insert(0, replacement);
        }
        else
        {
            dictionaries[dictionaries.IndexOf(current)] = replacement;
        }

        _logger.Debug($"Tema aplicado: {theme} ({effective}).");
    }

    public void ApplyTitleBar(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var value = _isDark ? 1 : 0;
        if (DwmSetWindowAttribute(handle, DarkModeAttribute, ref value, sizeof(int)) != 0)
        {
            DwmSetWindowAttribute(handle, DarkModeAttributeLegacy, ref value, sizeof(int));
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (CurrentTheme == AppTheme.System && e.Category == UserPreferenceCategory.General)
        {
            Application.Current?.Dispatcher.BeginInvoke(() => Apply(AppTheme.System));
        }
    }

    private static bool IsWindowsInDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
