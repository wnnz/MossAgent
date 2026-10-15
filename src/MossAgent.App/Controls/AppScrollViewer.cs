using Avalonia;
using Avalonia.Controls;

namespace MossAgent.App.Controls;

public sealed class AppScrollViewer : ScrollViewer
{
    public static readonly StyledProperty<object?> NavigationDragScopeProperty =
        AvaloniaProperty.Register<AppScrollViewer, object?>(nameof(NavigationDragScope));
    private AppNavigationDragScroller? _dragScroller;
    private bool _attached;

    public AppScrollViewer()
    {
        Classes.Add("app-scroll-viewer");
    }

    public object? NavigationDragScope
    {
        get => GetValue(NavigationDragScopeProperty);
        set => SetValue(NavigationDragScopeProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs args)
    {
        base.OnAttachedToVisualTree(args);
        _attached = true;
        if (NavigationDragScope is not null) _dragScroller = new AppNavigationDragScroller(this);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs args)
    {
        _attached = false;
        _dragScroller?.Dispose();
        _dragScroller = null;
        base.OnDetachedFromVisualTree(args);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != NavigationDragScopeProperty) return;
        _dragScroller?.Dispose();
        _dragScroller = NavigationDragScope is not null && _attached
            ? new AppNavigationDragScroller(this) : null;
    }

    protected override Type StyleKeyOverride => typeof(ScrollViewer);
}
