using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

namespace BuscaTabelas.App.Behaviors;

/// <summary>Mantém um ListBox rolado até o último item conforme novas linhas chegam (logs em tempo real).</summary>
public static class AutoScroll
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(AutoScroll), new PropertyMetadata(false, OnIsEnabledChanged));

    private static readonly DependencyProperty HandlerProperty = DependencyProperty.RegisterAttached(
        "Handler", typeof(NotifyCollectionChangedEventHandler), typeof(AutoScroll));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ListBox listBox)
        {
            return;
        }

        INotifyCollectionChanged items = listBox.Items;

        if (listBox.GetValue(HandlerProperty) is NotifyCollectionChangedEventHandler previous)
        {
            items.CollectionChanged -= previous;
            listBox.ClearValue(HandlerProperty);
        }

        if (e.NewValue is not true)
        {
            return;
        }

        NotifyCollectionChangedEventHandler handler = (_, args) =>
        {
            if (args.Action == NotifyCollectionChangedAction.Add && listBox.Items.Count > 0)
            {
                listBox.ScrollIntoView(listBox.Items[listBox.Items.Count - 1]);
            }
        };

        items.CollectionChanged += handler;
        listBox.SetValue(HandlerProperty, handler);
    }
}
