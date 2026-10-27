using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace BancoPronto.App.Converters;

/// <summary>Verdadeiro → Visible; falso → Collapsed. Com <see cref="Invert"/>, o contrário.</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        (value is true) != Invert ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
