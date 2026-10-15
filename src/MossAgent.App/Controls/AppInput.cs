using Avalonia.Controls;

namespace MossAgent.App.Controls;

public class AppInput : TextBox
{
    public AppInput()
    {
        Classes.Add("app-input");
    }

    protected override Type StyleKeyOverride => typeof(TextBox);
}
