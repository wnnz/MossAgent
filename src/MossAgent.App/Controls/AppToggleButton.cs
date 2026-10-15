using Avalonia.Controls.Primitives;

namespace MossAgent.App.Controls;

public class AppToggleButton : ToggleButton
{
    public AppToggleButton()
    {
        Classes.Add("app-toggle-button");
    }

    protected override Type StyleKeyOverride => typeof(ToggleButton);
}
