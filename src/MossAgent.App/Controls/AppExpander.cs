using Avalonia.Controls;

namespace MossAgent.App.Controls;

public sealed class AppExpander : Expander
{
    public AppExpander() => Classes.Add("app-expander");

    protected override Type StyleKeyOverride => typeof(Expander);
}
