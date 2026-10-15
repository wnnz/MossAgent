using Avalonia.Controls;

namespace MossAgent.App.Controls;

public sealed class AppViewbox : Viewbox
{
    protected override Type StyleKeyOverride => typeof(Viewbox);
}
