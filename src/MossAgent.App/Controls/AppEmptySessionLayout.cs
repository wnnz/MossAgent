using Avalonia;
using Avalonia.Controls;

namespace MossAgent.App.Controls;

public sealed class AppEmptySessionLayout : Panel
{
    public static readonly StyledProperty<double> BrandOffsetProperty =
        AvaloniaProperty.Register<AppEmptySessionLayout, double>(nameof(BrandOffset), validate: value => double.IsFinite(value) && value >= 0);

    static AppEmptySessionLayout() => AffectsArrange<AppEmptySessionLayout>(BrandOffsetProperty);

    public double BrandOffset { get => GetValue(BrandOffsetProperty); set => SetValue(BrandOffsetProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = 0d;
        var height = 0d;
        foreach (var child in Children)
        {
            child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
            width = Math.Max(width, child.DesiredSize.Width);
            height += child.DesiredSize.Height;
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count == 0) return finalSize;
        var height = Children.Sum(child => child.DesiredSize.Height);
        var brand = Children[0];
        var top = Math.Max(0, (finalSize.Height - height) / 2 - BrandOffset);
        brand.Arrange(new Rect(0, top, finalSize.Width, brand.DesiredSize.Height));
        var gap = Math.Clamp(finalSize.Height - height - top, 0, BrandOffset);
        top += brand.DesiredSize.Height + gap;
        for (var index = 1; index < Children.Count; index++)
        {
            var child = Children[index];
            child.Arrange(new Rect(0, top, finalSize.Width, child.DesiredSize.Height));
            top += child.DesiredSize.Height;
        }
        return finalSize;
    }
}
