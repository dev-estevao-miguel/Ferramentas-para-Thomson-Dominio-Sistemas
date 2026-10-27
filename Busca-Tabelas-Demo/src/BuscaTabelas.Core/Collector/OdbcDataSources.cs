using Microsoft.Win32;

namespace BuscaTabelas.Core.Collector;

/// <summary>Conexão ODBC (DSN) cadastrada no Windows.</summary>
/// <param name="Platform">"32 bits" ou "64 bits" (o coletor usa o PowerShell de 32 bits).</param>
/// <param name="Scope">"Sistema" ou "Usuário".</param>
public sealed record OdbcDataSource(string Name, string Driver, string Platform, string Scope)
{
    public bool Is32Bit => Platform == OdbcDataSources.Platform32;
}

/// <summary>Lê as conexões do SQL Anywhere direto do registro, sem abrir nenhuma conexão.</summary>
public static class OdbcDataSources
{
    public const string Platform32 = "32 bits";
    public const string Platform64 = "64 bits";

    private const string DataSourcesKey = @"SOFTWARE\ODBC\ODBC.INI\ODBC Data Sources";

    /// <returns>Conexões cujo driver é do SQL Anywhere, 32 bits primeiro.</returns>
    public static IReadOnlyList<OdbcDataSource> ListSqlAnywhere()
    {
        var sources = new List<OdbcDataSource>();
        Read(RegistryHive.LocalMachine, RegistryView.Registry32, Platform32, "Sistema", sources);
        if (Environment.Is64BitOperatingSystem)
        {
            Read(RegistryHive.LocalMachine, RegistryView.Registry64, Platform64, "Sistema", sources);
        }

        // DSNs de usuário ficam numa chave compartilhada pelas duas arquiteturas.
        Read(RegistryHive.CurrentUser, RegistryView.Default, Environment.Is64BitOperatingSystem ? "32/64 bits" : Platform32, "Usuário", sources);

        return sources
            .Where(s => s.Driver.Contains("SQL Anywhere", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(s => s.Is32Bit)
            .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void Read(RegistryHive hive, RegistryView view, string platform, string scope, List<OdbcDataSource> sources)
    {
        try
        {
            using var root = RegistryKey.OpenBaseKey(hive, view);
            using var key = root.OpenSubKey(DataSourcesKey);
            if (key is null)
            {
                return;
            }

            // Nome vazio é o valor "(Padrão)" da chave, não uma conexão.
            foreach (var name in key.GetValueNames().Where(n => !string.IsNullOrWhiteSpace(n)))
            {
                if (key.GetValue(name) is string driver)
                {
                    sources.Add(new OdbcDataSource(name, driver, platform, scope));
                }
            }
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            // Sem acesso ao registro: a conexão ainda pode ser digitada manualmente.
        }
    }
}
