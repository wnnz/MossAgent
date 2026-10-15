using Avalonia.Controls;

namespace MossAgent.App.Controls;

public sealed class AppCheckBox : CheckBox
{
    public AppCheckBox()
    {
        Classes.Add("app-check-box");
    }

    protected override Type StyleKeyOverride => typeof(CheckBox);
}
