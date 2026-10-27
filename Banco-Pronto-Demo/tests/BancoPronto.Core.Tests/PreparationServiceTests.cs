using BancoPronto.Core.Abstractions;
using BancoPronto.Core.Demo;
using BancoPronto.Core.Models;
using BancoPronto.Core.Preparation;
using BancoPronto.Core.Scanning;

namespace BancoPronto.Core.Tests;

public sealed class PreparationServiceTests : IDisposable
{
    private readonly BackupFolder _folder = new();
    private readonly DemoOptions _options;

    public PreparationServiceTests()
    {
        _options = new DemoOptions { StepDelay = TimeSpan.Zero, WorkRoot = _folder.WorkRoot };
    }

    public void Dispose() => _folder.Dispose();

    [Fact]
    public async Task Prepara_aplicando_o_completo_e_as_modificacoes_em_ordem()
    {
        var originals = new[]
        {
            _folder.Add("DEMO_20261001_0800_C.backup"),
            _folder.Add("DEMO_20261002_0900_M.backup"),
            _folder.Add("DEMO_20261003_1015_M.backup")
        };
        var before = originals.ToDictionary(p => p, File.ReadAllBytes);
        var progress = new RecordingProgress();

        var result = await CreateService().PrepareAsync(Scan(), progress);

        Assert.True(result.Succeeded, result.Error);
        Assert.NotNull(result.DatabasePath);
        Assert.StartsWith(_folder.WorkRoot, result.WorkFolder);
        Assert.Equal(
            ["DEMO_20261001_0800_C.backup", "DEMO_20261002_0900_M.backup", "DEMO_20261003_1015_M.backup"],
            DemoFormat.ReadApplied(result.DatabasePath));
        Assert.True(result.Validation!.IsValid);
        Assert.Equal(4, result.Validation.Checks.Count);
        Assert.All(originals, path => Assert.Equal(before[path], File.ReadAllBytes(path)));

        Assert.Equal(100, progress.Items[^1].Percent);
        Assert.Equal(PreparationStage.Completed, progress.Items[^1].Stage);
        Assert.Equal(progress.Items.Select(p => p.Percent).Order(), progress.Items.Select(p => p.Percent));
    }

    [Fact]
    public async Task Chama_as_etapas_na_ordem_extrair_aplicar_e_validar()
    {
        _folder.Add("DEMO_20261001_0800_C.backup");
        _folder.Add("DEMO_20261002_0900_M.backup");
        var steps = new List<string>();
        var service = new PreparationService(new RecordingExtractor(steps), new RecordingProcessor(steps), new RecordingValidator(steps), _options);

        var result = await service.PrepareAsync(Scan());

        Assert.True(result.Succeeded);
        Assert.Equal(
            ["extrair DEMO_20261001_0800_C.backup", "aplicar DEMO_20261001_0800_C.backup",
             "extrair DEMO_20261002_0900_M.backup", "aplicar DEMO_20261002_0900_M.backup", "validar"],
            steps);
    }

    [Fact]
    public async Task Arquivo_que_nao_e_backup_de_demonstracao_falha_com_mensagem_clara()
    {
        _folder.Write("DEMO_20261001_0800_C.backup", "conteudo qualquer");

        var result = await CreateService().PrepareAsync(Scan());

        Assert.False(result.Succeeded);
        Assert.Null(result.DatabasePath);
        Assert.Contains("DEMO_20261001_0800_C.backup", result.Error);
    }

    [Fact]
    public async Task Conteudo_de_outro_banco_e_recusado()
    {
        _folder.Add("DEMO_20261001_0800_C.backup", code: "OUTRO");

        var result = await CreateService().PrepareAsync(Scan());

        Assert.False(result.Succeeded);
        Assert.Contains("não pertence ao banco DEMO", result.Error);
    }

    [Fact]
    public async Task Cancelamento_interrompe_sem_gerar_resultado()
    {
        _folder.Add("DEMO_20261001_0800_C.backup");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateService().PrepareAsync(Scan(), null, cancellation.Token));
    }

    [Fact]
    public async Task Recusa_preparar_quando_a_verificacao_tem_erros()
    {
        _folder.Add("DEMO_20261002_0900_M.backup");

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().PrepareAsync(Scan()));
    }

    private BackupScanResult Scan() => new BackupScanner().Scan(_folder.Backups);

    private PreparationService CreateService() => new(
        new DemoBackupExtractor(_options),
        new DemoLogProcessor(_options),
        new DemoValidationService(_options),
        _options);

    /// <summary>IProgress síncrono: Progress&lt;T&gt; publicaria os eventos de forma assíncrona.</summary>
    private sealed class RecordingProgress : IProgress<PreparationProgress>
    {
        public List<PreparationProgress> Items { get; } = [];

        public void Report(PreparationProgress value) => Items.Add(value);
    }

    private sealed class RecordingExtractor(List<string> steps) : IBackupExtractor
    {
        public Task<string> ExtractAsync(BackupFile backup, string workFolder, CancellationToken cancellationToken)
        {
            steps.Add("extrair " + backup.FileName);
            return Task.FromResult(backup.Path);
        }
    }

    private sealed class RecordingProcessor(List<string> steps) : ILogProcessor
    {
        public Task ApplyAsync(string databasePath, BackupFile backup, string extractedPath, CancellationToken cancellationToken)
        {
            steps.Add("aplicar " + backup.FileName);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingValidator(List<string> steps) : IValidationService
    {
        public Task<ValidationReport> ValidateAsync(string databasePath, BackupScanResult backups, CancellationToken cancellationToken)
        {
            steps.Add("validar");
            return Task.FromResult(new ValidationReport(["ok"], []));
        }
    }
}
