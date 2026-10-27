namespace BancoPronto.Core.Demo;

/// <summary>Parâmetros da simulação.</summary>
public sealed class DemoOptions
{
    /// <summary>Pausa por etapa, para o progresso ser visível na interface. Zero nos testes.</summary>
    public TimeSpan StepDelay { get; init; } = TimeSpan.FromMilliseconds(700);

    /// <summary>Raiz das pastas de trabalho. Cada preparação cria uma subpasta nova.</summary>
    public string WorkRoot { get; init; } = Path.Combine(Path.GetTempPath(), "BancoProntoDemo");
}
