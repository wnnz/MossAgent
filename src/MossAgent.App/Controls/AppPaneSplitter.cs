using Avalonia.Automation;
using Avalonia.Controls;
using MossAgent.App.Resources;

namespace MossAgent.App.Controls;

public sealed class AppPaneSplitter : GridSplitter
{
    public AppPaneSplitter()
    {
        Classes.Add("app-pane-splitter");
        ResizeDirection = GridResizeDirection.Columns;
        ResizeBehavior = GridResizeBehavior.PreviousAndNext;
        AutomationProperties.SetName(this, SettingsText.Get("ResizeInspector"));
    }

    protected override Type StyleKeyOverride => typeof(GridSplitter);
}
