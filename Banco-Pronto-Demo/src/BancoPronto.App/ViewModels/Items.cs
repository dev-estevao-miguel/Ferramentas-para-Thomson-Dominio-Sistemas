using System.Globalization;
using BancoPronto.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BancoPronto.App.ViewModels;

public enum StepState
{
    Pending,
    Active,
    Done,
    Error
}

/// <summary>Etapa fixa do topo: Pasta · Verificação · Preparação · Banco pronto.</summary>
public sealed partial class StepItemViewModel : ObservableObject
{
    [ObservableProperty]
    private StepState _state;

    public StepItemViewModel(int number, string title)
    {
        Number = number;
        Title = title;
    }

    public int Number { get; }

    public string Title { get; }
}

/// <summary>Cor da etiqueta do backup na lista.</summary>
public enum BackupTone
{
    Full,
    Modification,
    Ignored
}

/// <summary>Linha da lista de backups identificados.</summary>
/// <param name="Order">Posição na sequência de aplicação; "—" para arquivos ignorados.</param>
public sealed record BackupItemViewModel(
    string Order,
    string KindText,
    BackupTone Tone,
    string FileName,
    string TimestampText,
    string SizeText,
    string? Note)
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("pt-BR");

    public bool HasNote => Note is not null;

    public static BackupItemViewModel From(BackupFile backup, string order, BackupTone tone, string? note = null) => new(
        order,
        backup.Kind == BackupKind.Full ? "Completo" : "Modificação",
        tone,
        backup.FileName,
        backup.Timestamp.ToString("dd/MM/yyyy HH:mm", Culture),
        FormatSize(backup.SizeBytes),
        note);

    private static string FormatSize(long bytes) =>
        bytes < 1024 ? $"{bytes} bytes" : (bytes / 1024.0).ToString("0.0", Culture) + " KB";
}

/// <summary>Linha do registro da preparação.</summary>
public sealed record LogLine(string Time, string Message);
