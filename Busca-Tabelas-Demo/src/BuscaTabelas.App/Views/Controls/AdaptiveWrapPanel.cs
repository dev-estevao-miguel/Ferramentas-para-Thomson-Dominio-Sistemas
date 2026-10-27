using System.Windows;
using System.Windows.Controls;

namespace BuscaTabelas.App.Views.Controls;

/// <summary>
/// Coloca os filhos lado a lado e quebra linha quando não cabem. Cada filho ocupa pelo menos o seu
/// <see cref="FrameworkElement.MinWidth"/>; a largura que sobra é dividida pelo peso (<see cref="WeightProperty"/>).
/// Todos os filhos de uma linha ficam com a mesma altura (cards alinhados).
/// Ex.: dois cards com MinWidth 380 ficam lado a lado na janela larga e empilhados a 960 px.
/// </summary>
public sealed class AdaptiveWrapPanel : Panel
{
    public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(
        nameof(Spacing), typeof(double), typeof(AdaptiveWrapPanel),
        new FrameworkPropertyMetadata(16.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty RowSpacingProperty = DependencyProperty.Register(
        nameof(RowSpacing), typeof(double), typeof(AdaptiveWrapPanel),
        new FrameworkPropertyMetadata(16.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty WeightProperty = DependencyProperty.RegisterAttached(
        "Weight", typeof(double), typeof(AdaptiveWrapPanel),
        new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsParentMeasure));

    /// <summary>Espaço horizontal entre filhos da mesma linha.</summary>
    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <summary>Espaço vertical entre linhas.</summary>
    public double RowSpacing
    {
        get => (double)GetValue(RowSpacingProperty);
        set => SetValue(RowSpacingProperty, value);
    }

    public static double GetWeight(UIElement element) => (double)element.GetValue(WeightProperty);

    public static void SetWeight(UIElement element, double value) => element.SetValue(WeightProperty, value);

    private readonly List<double> _rowHeights = [];

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? NaturalWidth() : availableSize.Width;
        _rowHeights.Clear();

        var height = 0.0;
        foreach (var row in BuildRows(width))
        {
            var widths = Distribute(row, width);
            var rowHeight = 0.0;
            for (var i = 0; i < row.Count; i++)
            {
                row[i].Measure(new Size(widths[i], double.PositiveInfinity));
                rowHeight = Math.Max(rowHeight, row[i].DesiredSize.Height);
            }

            height += (_rowHeights.Count > 0 ? RowSpacing : 0) + rowHeight;
            _rowHeights.Add(rowHeight);
        }

        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var y = 0.0;
        var rowIndex = 0;
        foreach (var row in BuildRows(finalSize.Width))
        {
            var widths = Distribute(row, finalSize.Width);
            var rowHeight = rowIndex < _rowHeights.Count ? _rowHeights[rowIndex] : row.Max(c => c.DesiredSize.Height);
            var x = 0.0;
            for (var i = 0; i < row.Count; i++)
            {
                row[i].Arrange(new Rect(x, y, widths[i], rowHeight));
                x += widths[i] + Spacing;
            }

            y += rowHeight + RowSpacing;
            rowIndex++;
        }

        return finalSize;
    }

    private IEnumerable<UIElement> VisibleChildren() =>
        InternalChildren.Cast<UIElement>().Where(child => child.Visibility != Visibility.Collapsed);

    private static double MinWidthOf(UIElement element) => element is FrameworkElement { MinWidth: > 0 } fe ? fe.MinWidth : 0;

    private double NaturalWidth()
    {
        var children = VisibleChildren().ToList();
        return children.Sum(MinWidthOf) + Math.Max(0, children.Count - 1) * Spacing;
    }

    /// <summary>Enche cada linha enquanto a soma das larguras mínimas (com os espaços) couber.</summary>
    private List<List<UIElement>> BuildRows(double width)
    {
        var rows = new List<List<UIElement>>();
        var current = new List<UIElement>();
        var used = 0.0;

        foreach (var child in VisibleChildren())
        {
            var needed = MinWidthOf(child) + (current.Count > 0 ? Spacing : 0);
            if (current.Count > 0 && used + needed > width)
            {
                rows.Add(current);
                current = [];
                needed = MinWidthOf(child);
                used = 0;
            }

            current.Add(child);
            used += needed;
        }

        if (current.Count > 0)
        {
            rows.Add(current);
        }

        return rows;
    }

    /// <summary>Divide a largura pelo peso; quem ficaria abaixo do mínimo recebe o mínimo e o resto é redividido.</summary>
    private double[] Distribute(List<UIElement> row, double width)
    {
        var widths = new double[row.Count];
        var available = Math.Max(0, width - (row.Count - 1) * Spacing);
        if (row.Count == 1)
        {
            widths[0] = available;
            return widths;
        }

        var fixedAtMin = new bool[row.Count];
        bool changed;
        do
        {
            changed = false;
            var remaining = available - Enumerable.Range(0, row.Count).Where(i => fixedAtMin[i]).Sum(i => widths[i]);
            var totalWeight = Enumerable.Range(0, row.Count).Where(i => !fixedAtMin[i]).Sum(i => Math.Max(0.0001, GetWeight(row[i])));

            for (var i = 0; i < row.Count; i++)
            {
                if (fixedAtMin[i])
                {
                    continue;
                }

                widths[i] = Math.Max(0, remaining * Math.Max(0.0001, GetWeight(row[i])) / totalWeight);
                if (widths[i] < MinWidthOf(row[i]))
                {
                    widths[i] = MinWidthOf(row[i]);
                    fixedAtMin[i] = true;
                    changed = true;
                }
            }
        }
        while (changed);

        return widths;
    }
}
