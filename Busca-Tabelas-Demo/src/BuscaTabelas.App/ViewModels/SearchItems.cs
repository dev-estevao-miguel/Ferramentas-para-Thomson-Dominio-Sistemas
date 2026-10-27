using CommunityToolkit.Mvvm.ComponentModel;
using BuscaTabelas.Core.Catalog;

namespace BuscaTabelas.App.ViewModels;

/// <summary>Linha da lista de resultados.</summary>
public sealed class SearchHitItem(CatalogSearchHit hit)
{
    public CatalogSearchHit Hit { get; } = hit;

    /// <summary>"Coluna", ou o tipo da tabela (Tabela, View, Temporária global).</summary>
    public string KindText => Hit.IsColumn ? "Coluna" : Hit.Table.TypeDisplay;

    public bool IsColumn => Hit.IsColumn;

    public string FullName => Hit.FullName;

    public string DescriptionText => string.IsNullOrWhiteSpace(Hit.Description) ? "Sem descrição no catálogo" : Hit.Description;

    public bool HasDescription => !string.IsNullOrWhiteSpace(Hit.Description);
}

/// <summary>Coluna da tabela aberta. A seleção múltipla (Ctrl/Shift) monta o SELECT.</summary>
public sealed partial class ColumnItem(CatalogColumn column) : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    public CatalogColumn Column { get; } = column;

    public string Name => Column.Name;

    public string Type => Column.Type;

    public string SizeText => Column.Size?.ToString() ?? string.Empty;

    public string ScaleText => Column.Scale is null or 0 ? string.Empty : Column.Scale.Value.ToString();

    public bool IsPrimaryKey => Column.IsPrimaryKey;

    public string NullText => Column.AllowsNull switch
    {
        true => "Sim",
        false => "Não",
        null => string.Empty
    };

    public string Description => Column.Description;

    public bool HasCodes => Column.Codes.Count > 0;

    public string CodesText => Column.Codes.Count == 1 ? "1 código" : $"{Column.Codes.Count} códigos";

    public string DescriptionText => Column.HasDescription ? Column.Description : "Sem descrição no catálogo.";

    public string DefaultValueText => Column.DefaultValue ?? "(não informado)";

    /// <summary>"integer", "varchar(60)", "numeric(15,2)".</summary>
    public string TypeText => Column switch
    {
        { Size: null } => Column.Type,
        { Scale: > 0 } => $"{Column.Type}({Column.Size},{Column.Scale})",
        _ => $"{Column.Type}({Column.Size})"
    };
}
