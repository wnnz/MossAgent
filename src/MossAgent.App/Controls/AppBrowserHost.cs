using Avalonia;
using Avalonia.Controls;

namespace MossAgent.App.Controls;

public sealed class AppBrowserHost : Panel
{
    public static readonly StyledProperty<bool> IsPresentationBlockedProperty =
        AvaloniaProperty.Register<AppBrowserHost, bool>(nameof(IsPresentationBlocked));

    private AppBrowserViewport? _viewport;
    private Control? _activeView;
    private Rect? _activeBounds;
    private Size _parkingSize = new(1024, 768);

    public bool IsPresentationBlocked
    {
        get => GetValue(IsPresentationBlockedProperty);
        set => SetValue(IsPresentationBlockedProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsPresentationBlockedProperty) InvalidateMeasure();
    }

    public void Register(Control view)
    {
        if (!Children.Contains(view)) Children.Add(view);
    }

    public void Unregister(Control view)
    {
        Children.Remove(view);
        if (ReferenceEquals(_activeView, view))
        {
            _activeView = null;
            _activeBounds = null;
        }
        InvalidateMeasure();
    }

    internal void UpdateViewport(AppBrowserViewport viewport, Control? view, Rect? bounds)
    {
        _viewport = viewport;
        if (ReferenceEquals(_activeView, view) && _activeBounds == bounds) return;
        _activeView = view;
        _activeBounds = bounds;
        if (bounds is { Width: > 0, Height: > 0 } rectangle) _parkingSize = rectangle.Size;
        InvalidateMeasure();
    }

    internal void ClearViewport(AppBrowserViewport viewport)
    {
        if (!ReferenceEquals(_viewport, viewport)) return;
        _viewport = null;
        _activeView = null;
        _activeBounds = null;
        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var view in Children)
            view.Measure(ReferenceEquals(view, _activeView) && _activeBounds is { } bounds
                ? bounds.Size : _parkingSize);
        return default;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var view in Children)
        {
            var active = !IsPresentationBlocked && ReferenceEquals(view, _activeView) && _activeBounds.HasValue;
            view.IsHitTestVisible = active;
            view.Focusable = active;
            var bounds = active ? _activeBounds!.Value
                : new Rect(-_parkingSize.Width - 32, -_parkingSize.Height - 32,
                    _parkingSize.Width, _parkingSize.Height);
            view.Arrange(bounds);
            if (view is NativeControlHost native) native.TryUpdateNativeControlPosition();
        }
        return finalSize;
    }
}
