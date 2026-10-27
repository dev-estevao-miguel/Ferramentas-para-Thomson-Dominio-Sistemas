using System.Text.Json;
using System.Text.Json.Serialization;
using BuscaTabelas.Core.Logging;

namespace BuscaTabelas.Core.Configuration;

public sealed class JsonConfigurationService : IConfigurationService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _filePath;
    private readonly ILoggingService _logger;
    private readonly object _sync = new();

    public JsonConfigurationService(string filePath, ILoggingService logger)
    {
        _filePath = filePath;
        _logger = logger;
        Current = Load();
    }

    public AppSettings Current { get; private set; }

    public event EventHandler<AppSettings>? SettingsChanged;

    public void Save(AppSettings settings)
    {
        var snapshot = settings.Clone();

        lock (_sync)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);

            // Grava em arquivo temporário e substitui, para nunca deixar um settings.json pela metade.
            var tempFile = _filePath + ".tmp";
            File.WriteAllText(tempFile, JsonSerializer.Serialize(snapshot, SerializerOptions));
            File.Move(tempFile, _filePath, overwrite: true);

            Current = snapshot;
        }

        _logger.Debug("Configurações salvas.");
        SettingsChanged?.Invoke(this, snapshot);
    }

    public void Update(Action<AppSettings> change)
    {
        var copy = Current.Clone();
        change(copy);
        Save(copy);
    }

    private AppSettings Load()
    {
        if (!File.Exists(_filePath))
        {
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _logger.Warning("Não foi possível ler as configurações; os valores padrão serão usados.", ex);
            TryPreserveInvalidFile();
            return new AppSettings();
        }
    }

    private void TryPreserveInvalidFile()
    {
        try
        {
            File.Copy(_filePath, $"{_filePath}.invalid-{DateTime.Now:yyyyMMddHHmmss}", overwrite: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
