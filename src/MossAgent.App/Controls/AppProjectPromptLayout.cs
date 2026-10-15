using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace MossAgent.App.Controls;

internal sealed class AppProjectPromptLayout : Panel
{
    private readonly AppText _prefix;
    private readonly AppButton _project;
    private readonly AppText _suffix;
    private int _rows;

    public AppProjectPromptLayout(AppText prefix, AppButton project, AppText suffix)
    {
        _prefix = prefix;
        _project = project;
        _suffix = suffix;
        Classes.Add("project-prompt-layout");
        Children.Add(prefix);
        Children.Add(project);
        Children.Add(suffix);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children) child.Measure(Size.Infinity);
        var width = ChildSize(_prefix).Width + ChildSize(_project).Width + ChildSize(_suffix).Width;
        if (width <= availableSize.Width)
        {
            _rows = 1;
            return new Size(width, Children.Max(child => ChildSize(child).Height));
        }
        _suffix.Measure(new Size(availableSize.Width, double.PositiveInfinity));
        if (ChildSize(_prefix).Width < availableSize.Width)
        {
            _rows = 2;
            _project.Measure(new Size(availableSize.Width - ChildSize(_prefix).Width, double.PositiveInfinity));
            return new Size(Math.Max(ChildSize(_prefix).Width + ChildSize(_project).Width, ChildSize(_suffix).Width),
                Math.Max(ChildSize(_prefix).Height, ChildSize(_project).Height) + ChildSize(_suffix).Height);
        }
        _rows = 3;
        _prefix.Measure(new Size(availableSize.Width, double.PositiveInfinity));
        _project.Measure(new Size(availableSize.Width, double.PositiveInfinity));
        return new Size(Children.Max(child => ChildSize(child).Width), Children.Sum(child => ChildSize(child).Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_rows == 1)
        {
            var left = RowLeft(finalSize.Width, Children.Sum(child => ChildSize(child).Width));
            foreach (var child in Children)
            {
                ArrangeCentered(child, left, 0, ChildSize(child).Width, finalSize.Height);
                left += ChildSize(child).Width;
            }
        }
        else if (_rows == 2)
        {
            var height = Math.Max(ChildSize(_prefix).Height, ChildSize(_project).Height);
            var left = RowLeft(finalSize.Width, ChildSize(_prefix).Width + ChildSize(_project).Width);
            ArrangeCentered(_prefix, left, 0, ChildSize(_prefix).Width, height);
            ArrangeCentered(_project, left + ChildSize(_prefix).Width, 0, ChildSize(_project).Width, height);
            ArrangeCentered(_suffix, 0, height, finalSize.Width, ChildSize(_suffix).Height);
        }
        else
        {
            var top = 0d;
            foreach (var child in Children)
            {
                ArrangeCentered(child, 0, top, finalSize.Width, ChildSize(child).Height);
                top += ChildSize(child).Height;
            }
        }
        return finalSize;
    }

    private static void ArrangeCentered(Control child, double left, double top, double width, double height) =>
        child.Arrange(new Rect(left, top, width, height));

    private static Size ChildSize(Control child) => child.UseLayoutRounding
        ? LayoutHelper.RoundLayoutSizeUp(child.DesiredSize, LayoutHelper.GetLayoutScale(child)) : child.DesiredSize;

    private double RowLeft(double availableWidth, double width)
    {
        var left = (availableWidth - width) / 2;
        return UseLayoutRounding ? LayoutHelper.RoundLayoutValue(left, LayoutHelper.GetLayoutScale(this)) : left;
    }
}
