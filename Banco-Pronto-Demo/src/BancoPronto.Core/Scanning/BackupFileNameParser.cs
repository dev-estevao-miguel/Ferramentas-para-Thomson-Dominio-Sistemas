using System.Globalization;
using System.Text.RegularExpressions;
using BancoPronto.Core.Models;

namespace BancoPronto.Core.Scanning;

/// <summary>
/// Lê o padrão de nome dos backups de demonstração: <c>&lt;BANCO&gt;_&lt;aaaaMMdd&gt;_&lt;HHmm&gt;_&lt;C|M&gt;.backup</c>,
/// ex.: <c>DEMO_20261001_0800_C.backup</c> (completo) e <c>DEMO_20261002_0900_M.backup</c> (modificação).
/// </summary>
public static class BackupFileNameParser
{
    public const string Extension = ".backup";

    public const string PatternDescription = "<BANCO>_<aaaaMMdd>_<HHmm>_<C|M>.backup";

    private static readonly Regex Pattern = new(
        @"^(?<code>[A-Za-z0-9]+)_(?<date>\d{8})_(?<time>\d{4})_(?<kind>[CcMm])\.backup$",
        RegexOptions.CultureInvariant);

    /// <returns>Nulo quando o nome não segue o padrão ou a data/hora não existe (ex.: 31/02).</returns>
    public static BackupFileName? Parse(string fileName)
    {
        var match = Pattern.Match(fileName);
        if (!match.Success)
        {
            return null;
        }

        if (!DateTime.TryParseExact(
                match.Groups["date"].Value + match.Groups["time"].Value,
                "yyyyMMddHHmm",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var timestamp))
        {
            return null;
        }

        var kind = char.ToUpperInvariant(match.Groups["kind"].Value[0]) == 'C' ? BackupKind.Full : BackupKind.Modification;
        return new BackupFileName(match.Groups["code"].Value.ToUpperInvariant(), timestamp, kind);
    }
}
