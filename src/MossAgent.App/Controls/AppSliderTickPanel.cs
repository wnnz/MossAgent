using Avalonia;
using Avalonia.Controls;

namespace MossAgent.App.Controls;

public sealed class AppSliderTickPanel : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children) child.Measure(availableSize);
        return new Size(0, Children.Select(child => child.DesiredSize.Height).DefaultIfEmpty().Max());
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        for (var index = 0; index < Children.Count; index++)
        {
            var child = Children[index];
            var size = child.DesiredSize;
            var offset = Children.Count < 2 ? (finalSize.Width - size.Width) / 2
                : (finalSize.Width - size.Width) * index / (Children.Count - 1);
            child.Arrange(new Rect(offset, (finalSize.Height - size.Height) / 2, size.Width, size.Height));
        }
        return finalSize;
    }
}
