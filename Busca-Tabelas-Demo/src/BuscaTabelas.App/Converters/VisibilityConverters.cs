using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace BuscaTabelas.App.Converters;

/// <summary>true → Visible. Com <see cref="Invert"/>, false → Visible.</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        (value is true) ^ Invert ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        (value is Visibility.Visible) ^ Invert;
}

/// <summary>Nulo, texto vazio ou coleção vazia → Collapsed. Com <see cref="Invert"/>, o contrário.</summary>
public sealed class EmptyToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isEmpty = value switch
        {
            null => true,
            string text => string.IsNullOrWhiteSpace(text),
            int count => count == 0,
            ICollection collection => collection.Count == 0,
            _ => false
        };

        return isEmpty ^ Invert ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Visible quando o enum é igual ao parâmetro (aceita vários valores separados por "|").</summary>
public sealed class EnumToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is not string expected)
        {
            return Visibility.Collapsed;
        }

        var current = value.ToString();
        return expected.Split('|').Any(e => e.Trim() == current) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
