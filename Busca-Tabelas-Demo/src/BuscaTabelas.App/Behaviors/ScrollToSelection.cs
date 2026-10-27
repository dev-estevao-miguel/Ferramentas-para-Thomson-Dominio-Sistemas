using System.Windows;
using System.Windows.Controls;

namespace BuscaTabelas.App.Behaviors;

/// <summary>
/// Rola um ListBox até o item selecionado quando a seleção passa a ter um único item
/// (ex.: resultado do tipo coluna abre a tabela já com a coluna marcada e visível).
/// </summary>
public static class ScrollToSelection
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(ScrollToSelection), new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ListBox listBox)
        {
            return;
        }

        listBox.SelectionChanged -= OnSelectionChanged;
        if (e.NewValue is true)
        {
            listBox.SelectionChanged += OnSelectionChanged;
        }
    }

    private static void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox { SelectedItems.Count: 1 } listBox && e.AddedItems.Count > 0)
        {
            // Depois do layout: a lista pode ter acabado de receber os itens.
            listBox.Dispatcher.BeginInvoke(() => listBox.ScrollIntoView(listBox.SelectedItems[0]!));
        }
    }
}
