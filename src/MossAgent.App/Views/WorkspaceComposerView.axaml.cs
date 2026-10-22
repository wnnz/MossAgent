using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

using MossAgent.App.Controls;

namespace MossAgent.App.Views;

public sealed partial class WorkspaceComposerView : AppUserControl
{
    private const double CompactWindowHeight = 720;
    private Window? _hostWindow;
    private bool _isToolbarStacked;

    public WorkspaceComposerView() => InitializeComponent();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs attachment)
    {
        base.OnAttachedToVisualTree(attachment);
        _hostWindow = TopLevel.GetTopLevel(this) as Window;
        if (_hostWindow is null) return;

        _hostWindow.SizeChanged += HandleWindowSizeChanged;
        ApprovalPolicyButton.PropertyChanged += HandlePolicyVisibilityChanged;
        UpdateComposerMode();
        UpdateToolbarLayout();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs attachment)
    {
        ApprovalPolicyButton.PropertyChanged -= HandlePolicyVisibilityChanged;
        if (_hostWindow is not null)
        {
            _hostWindow.SizeChanged -= HandleWindowSizeChanged;
            _hostWindow = null;
        }
        base.OnDetachedFromVisualTree(attachment);
    }

    private void HandleWindowSizeChanged(object? sender, SizeChangedEventArgs sizeEvent) => UpdateComposerMode();

    private void HandleToolbarSizeChanged(object? sender, SizeChangedEventArgs sizeEvent) => UpdateToolbarLayout();

    private void HandlePolicyVisibilityChanged(object? sender, AvaloniaPropertyChangedEventArgs propertyEvent)
    {
        if (propertyEvent.Property == IsVisibleProperty) UpdateToolbarLayout();
    }

    private void UpdateToolbarLayout()
    {
        var stacked = ComposerToolbar.Bounds.Width < 360 && ApprovalPolicyButton.IsVisible;
        if (_isToolbarStacked == stacked) return;

        _isToolbarStacked = stacked;
        ComposerToolbar.Classes.Set("stacked", stacked);
        Grid.SetRow(ModelPickerButton, stacked ? 1 : 0);
        Grid.SetColumn(ModelPickerButton, stacked ? 0 : 2);
        Grid.SetColumnSpan(ModelPickerButton, stacked ? 3 : 1);
        Grid.SetRow(StopButton, stacked ? 1 : 0);
        Grid.SetRow(SendButton, stacked ? 1 : 0);
        Grid.SetRow(QueueButton, stacked ? 1 : 0);
    }

    private void UpdateComposerMode()
    {
        if (_hostWindow is null) return;
        var compact = _hostWindow.Bounds.Height < CompactWindowHeight;
        if (ComposerRoot.Classes.Contains("compact-composer") == compact) return;

        if (compact) ComposerRoot.Classes.Add("compact-composer");
        else ComposerRoot.Classes.Remove("compact-composer");
    }
}
