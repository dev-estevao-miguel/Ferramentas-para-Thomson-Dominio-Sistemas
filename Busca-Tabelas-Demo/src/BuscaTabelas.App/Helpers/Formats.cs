using System.Globalization;

namespace BuscaTabelas.App.Helpers;

/// <summary>Números e datas no padrão brasileiro (5.139; 14/10/2026 11:18).</summary>
public static class Formats
{
    public static CultureInfo Culture { get; } = CultureInfo.GetCultureInfo("pt-BR");

    public static string Number(long value) => value.ToString("N0", Culture);

    public static string DateTime(DateTime value) => value.ToString("dd/MM/yyyy HH:mm", Culture);

    public static string DateTime(DateTimeOffset? value) => value is { } date ? DateTime(date.LocalDateTime) : "não informado";

    public static string FileSize(long bytes) => bytes switch
    {
        >= 1024 * 1024 => (bytes / 1024d / 1024d).ToString("0.0", Culture) + " MB",
        >= 1024 => (bytes / 1024d).ToString("0", Culture) + " KB",
        _ => bytes + " bytes"
    };

    /// <summary>"1 coluna" / "2 colunas".</summary>
    public static string Count(long value, string singular, string plural) =>
        $"{Number(value)} {(value == 1 ? singular : plural)}";
}
