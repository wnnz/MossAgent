using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace MossAgent.App.Controls;

public sealed class AppBrowserViewport : Control
{
    public static readonly StyledProperty<AppBrowserHost?> HostProperty =
        AvaloniaProperty.Register<AppBrowserViewport, AppBrowserHost?>(nameof(Host));
    public static readonly StyledProperty<Control?> ViewProperty =
        AvaloniaProperty.Register<AppBrowserViewport, Control?>(nameof(View));

    private AppBrowserHost? _attachedHost;
    private bool _attached;
    private readonly List<Visual> _ancestors = [];

    public AppBrowserHost? Host
    {
        get => GetValue(HostProperty);
        set => SetValue(HostProperty, value);
    }

    public Control? View
    {
        get => GetValue(ViewProperty);
        set => SetValue(ViewProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs args)
    {
        base.OnAttachedToVisualTree(args);
        _attached = true;
        LayoutUpdated += HandleLayoutUpdated;
        foreach (var ancestor in this.GetVisualAncestors())
        {
            _ancestors.Add(ancestor);
            ancestor.PropertyChanged += HandleAncestorPlacementChanged;
        }
        RefreshPlacement();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs args)
    {
        _attached = false;
        LayoutUpdated -= HandleLayoutUpdated;
        foreach (var ancestor in _ancestors) ancestor.PropertyChanged -= HandleAncestorPlacementChanged;
        _ancestors.Clear();
        _attachedHost?.ClearViewport(this);
        _attachedHost = null;
        base.OnDetachedFromVisualTree(args);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HostProperty || change.Property == ViewProperty
            || change.Property == BoundsProperty || change.Property == IsVisibleProperty)
            RefreshPlacement();
    }

    private void HandleLayoutUpdated(object? sender, EventArgs args) => RefreshPlacement();

    private void HandleAncestorPlacementChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == BoundsProperty || change.Property == IsVisibleProperty
            || change.Property == RenderTransformProperty || change.Property == RenderTransformOriginProperty)
            RefreshPlacement();
    }

    private void RefreshPlacement()
    {
        if (!_attached) return;
        if (!ReferenceEquals(_attachedHost, Host))
        {
            _attachedHost?.ClearViewport(this);
            _attachedHost = Host;
        }
        if (_attachedHost is null) return;
        Rect? rectangle = IsEffectivelyVisible && Bounds.Width > 0 && Bounds.Height > 0
            && this.TranslatePoint(default, _attachedHost) is { } origin
                ? new Rect(origin, Bounds.Size) : null;
        _attachedHost.UpdateViewport(this, rectangle.HasValue ? View : null, rectangle);
    }
}
