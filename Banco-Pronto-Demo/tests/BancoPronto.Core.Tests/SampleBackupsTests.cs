using BancoPronto.Core.Demo;
using BancoPronto.Core.Models;
using BancoPronto.Core.Preparation;
using BancoPronto.Core.Scanning;

namespace BancoPronto.Core.Tests;

/// <summary>Os arquivos fictícios de samples/ precisam demonstrar o fluxo completo sem ajustes.</summary>
public sealed class SampleBackupsTests : IDisposable
{
    private static readonly string SamplesFolder = Path.Combine(AppContext.BaseDirectory, "samples", "backups-demo");

    private readonly string _workRoot = Path.Combine(Path.GetTempPath(), "bp-amostras-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_workRoot))
        {
            Directory.Delete(_workRoot, recursive: true);
        }
    }

    [Fact]
    public void Amostras_formam_um_completo_e_duas_modificacoes()
    {
        var result = new BackupScanner().Scan(SamplesFolder);

        Assert.True(result.CanPrepare);
        Assert.Empty(result.Issues);
        Assert.Equal(BackupKind.Full, result.Full!.Kind);
        Assert.Equal(2, result.Modifications.Count);
    }

    [Fact]
    public async Task Amostras_sao_preparadas_e_validadas()
    {
        var options = new DemoOptions { StepDelay = TimeSpan.Zero, WorkRoot = _workRoot };
        var service = new PreparationService(
            new DemoBackupExtractor(options), new DemoLogProcessor(options), new DemoValidationService(options), options);

        var result = await service.PrepareAsync(new BackupScanner().Scan(SamplesFolder));

        Assert.True(result.Succeeded, result.Error);
        Assert.True(result.Validation!.IsValid);
    }
}
