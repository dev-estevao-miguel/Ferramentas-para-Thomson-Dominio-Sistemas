namespace BuscaTabelas.Core.Configuration;

public interface IConfigurationService
{
    /// <summary>Configuração atual. Trate como somente leitura; altere via <see cref="Save"/> ou <see cref="Update"/>.</summary>
    AppSettings Current { get; }

    event EventHandler<AppSettings>? SettingsChanged;

    void Save(AppSettings settings);

    void Update(Action<AppSettings> change);
}
