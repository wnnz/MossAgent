using Avalonia.Controls;

namespace MossAgent.App.Controls;

public sealed class AppListBox : ListBox
{
    public AppListBox()
    {
        Classes.Add("app-list-box");
    }

    protected override Type StyleKeyOverride => typeof(ListBox);
}
