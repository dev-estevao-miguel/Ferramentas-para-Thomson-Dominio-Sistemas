using BancoPronto.Core.Demo;

namespace BancoPronto.Core.Tests;

/// <summary>Pasta temporária com backups fictícios, apagada ao final de cada teste.</summary>
public sealed class BackupFolder : IDisposable
{
    public BackupFolder()
    {
        Root = Path.Combine(Path.GetTempPath(), "bp-testes-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Backups);
    }

    public string Root { get; }

    public string Backups => Path.Combine(Root, "backups");

    public string WorkRoot => Path.Combine(Root, "trabalho");

    /// <summary>Cria um backup de demonstração válido para o banco <paramref name="code"/>.</summary>
    public string Add(string fileName, string code = "DEMO")
    {
        string[] lines = [DemoFormat.BackupMarker + " - teste", $"banco={code}", "tipo=teste"];
        return Write(fileName, lines);
    }

    public string Write(string fileName, params string[] lines)
    {
        var path = Path.Combine(Backups, fileName);
        File.WriteAllLines(path, lines);
        return path;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
