using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

using MossAgent.App.Controls;

namespace MossAgent.App.Views;

public sealed partial class WorkspaceComposerView : AppUserControl
{
    private const double CompactWindowHeight = 720;
    private Window? _hostWindow;

    public WorkspaceComposerView() => InitializeComponent();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs attachment)
    {
        base.OnAttachedToVisualTree(attachment);
        _hostWindow = TopLevel.GetTopLevel(this) as Window;
        if (_hostWindow is null) return;

        _hostWindow.SizeChanged += HandleWindowSizeChanged;
        UpdateComposerMode();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs attachment)
    {
        if (_hostWindow is not null)
        {
            _hostWindow.SizeChanged -= HandleWindowSizeChanged;
            _hostWindow = null;
        }
        base.OnDetachedFromVisualTree(attachment);
    }

    private void HandleWindowSizeChanged(object? sender, SizeChangedEventArgs sizeEvent) => UpdateComposerMode();

    private void UpdateComposerMode()
    {
        if (_hostWindow is null) return;
        var compact = _hostWindow.Bounds.Height < CompactWindowHeight;
        if (ComposerRoot.Classes.Contains("compact-composer") == compact) return;

        if (compact) ComposerRoot.Classes.Add("compact-composer");
        else ComposerRoot.Classes.Remove("compact-composer");
    }
}
