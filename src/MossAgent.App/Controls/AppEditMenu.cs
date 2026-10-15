using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Resources;

namespace MossAgent.App.Controls;

public sealed class AppEditMenu : AppButton
{
    private readonly AppCard _surface;
    private readonly AppFlyout _flyout;
    private readonly Dictionary<TextEditingAction, AppActionButton> _rows = [];
    private readonly RelayCommand _unavailable = new(() => { }, () => false);
    private TextEditingCommands? _editing;

    public AppEditMenu()
    {
        Classes.Add("ghost");
        Classes.Add("menu-trigger");
        Content = WindowMenuText.Get("Edit");
        var layout = new StackPanel();
        AddRow(layout, TextEditingAction.Undo, "Ctrl+Z");
        AddRow(layout, TextEditingAction.Redo, "Ctrl+Y");
        layout.Children.Add(new AppBorder { Classes = { "divider-fill", "edit-menu-divider" } });
        AddRow(layout, TextEditingAction.Cut, "Ctrl+X");
        AddRow(layout, TextEditingAction.Copy, "Ctrl+C");
        AddRow(layout, TextEditingAction.Paste, "Ctrl+V");
        AddRow(layout, TextEditingAction.SelectAll, "Ctrl+A");
        _surface = new AppCard { Classes = { "flyout-surface", "edit-menu-surface" }, Child = layout };
        _flyout = new AppFlyout
        {
            Placement = PlacementMode.BottomEdgeAlignedLeft,
            DismissOnButtonClick = true,
            Content = _surface
        };
        _flyout.Opening += (_, _) => _editing?.PrepareForMenu();
        Flyout = _flyout;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs args)
    {
        base.OnAttachedToVisualTree(args);
        if (TopLevel.GetTopLevel(this) is not Window window) return;
        _editing?.Dispose();
        _editing = new TextEditingCommands(window, IsMenuFocus);
        foreach (var row in _rows) row.Value.Command = _editing[row.Key];
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs args)
    {
        _flyout.Hide();
        _editing?.Dispose();
        _editing = null;
        foreach (var row in _rows.Values) row.Command = _unavailable;
        base.OnDetachedFromVisualTree(args);
    }

    private bool IsMenuFocus(Control control) => ReferenceEquals(control, this)
        || this.IsVisualAncestorOf(control) || ReferenceEquals(control, _surface) || _surface.IsVisualAncestorOf(control);

    private void AddRow(StackPanel layout, TextEditingAction action, string shortcut)
    {
        var row = new AppActionButton
        {
            Classes = { "ghost", "edit-menu-action" },
            Label = WindowMenuText.Get(action.ToString()),
            BadgeText = shortcut,
            Command = _unavailable
        };
        _rows.Add(action, row);
        layout.Children.Add(row);
    }

    protected override Type StyleKeyOverride => typeof(AppButton);
}
