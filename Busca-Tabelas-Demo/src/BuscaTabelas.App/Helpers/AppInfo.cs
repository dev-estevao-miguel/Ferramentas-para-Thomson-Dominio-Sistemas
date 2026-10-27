using System.Reflection;

namespace BuscaTabelas.App.Helpers;

/// <summary>Nome e versão vêm dos atributos do assembly (Directory.Build.props), sem valores fixos no código.</summary>
public static class AppInfo
{
    private static readonly Assembly EntryAssembly = typeof(AppInfo).Assembly;

    public static string Name { get; } =
        EntryAssembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "Busca Tabelas";

    /// <summary>Nome curto usado nos textos da interface.</summary>
    public const string ShortName = "B.T";

    public static string Version { get; } =
        EntryAssembly.GetName().Version is { } version ? version.ToString(3) : "0.0.0";
}
