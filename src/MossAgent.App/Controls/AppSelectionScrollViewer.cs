using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace MossAgent.App.Controls;

public sealed class AppSelectionScrollViewer : ScrollViewer
{
    public static readonly StyledProperty<object?> SelectedItemProperty =
        AvaloniaProperty.Register<AppSelectionScrollViewer, object?>(nameof(SelectedItem));

    private bool _revealPending;

    public AppSelectionScrollViewer()
    {
        Classes.Add("app-scroll-viewer");
        HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;
        VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
    }

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(ScrollViewer);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs args)
    {
        base.OnAttachedToVisualTree(args);
        LayoutUpdated += HandleLayoutUpdated;
        _revealPending = true;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs args)
    {
        LayoutUpdated -= HandleLayoutUpdated;
        _revealPending = false;
        base.OnDetachedFromVisualTree(args);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedItemProperty || change.Property == ViewportProperty
            || change.Property == ContentProperty || change.Property == IsVisibleProperty)
            _revealPending = true;
    }

    private void HandleLayoutUpdated(object? sender, EventArgs args)
    {
        if (!_revealPending || SelectedItem is null || Viewport.Width <= 0) return;
        var selected = this.GetVisualDescendants().OfType<Control>().FirstOrDefault(control =>
            ReferenceEquals(control.DataContext, SelectedItem) && control.Bounds.Width > 0);
        if (selected?.TranslatePoint(default, this) is not { } point) return;

        _revealPending = false;
        var offset = CalculateHorizontalOffset(Offset.X, Viewport.Width, Extent.Width,
            point.X + Offset.X, selected.Bounds.Width);
        Offset = new Vector(offset, Offset.Y);
    }

    internal static double CalculateHorizontalOffset(double offset, double viewportWidth,
        double extentWidth, double itemStart, double itemWidth)
    {
        if (!double.IsFinite(offset) || !double.IsFinite(viewportWidth) || !double.IsFinite(extentWidth)
            || !double.IsFinite(itemStart) || !double.IsFinite(itemWidth)
            || viewportWidth <= 0 || itemWidth <= 0 || extentWidth < 0) return offset;

        var target = offset;
        if (itemWidth >= viewportWidth || itemStart < offset) target = itemStart;
        else if (itemStart + itemWidth > offset + viewportWidth)
            target = itemStart + itemWidth - viewportWidth;
        return Math.Clamp(target, 0, Math.Max(0, extentWidth - viewportWidth));
    }
}
