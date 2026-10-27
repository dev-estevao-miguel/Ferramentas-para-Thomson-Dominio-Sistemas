using System.Globalization;
using System.Windows.Data;

namespace BuscaTabelas.App.Converters;

/// <summary>Liga um enum a RadioButtons: IsChecked = (valor == parâmetro).</summary>
public sealed class EnumToBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null && parameter is string expected && value.ToString() == expected;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true && parameter is string expected
            ? Enum.Parse(targetType, expected)
            : Binding.DoNothing;
}
