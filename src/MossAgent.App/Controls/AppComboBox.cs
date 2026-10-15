using Avalonia.Controls;

namespace MossAgent.App.Controls;

public class AppComboBox : ComboBox
{
    public AppComboBox()
    {
        Classes.Add("app-combo-box");
    }

    protected override Type StyleKeyOverride => typeof(ComboBox);
}
