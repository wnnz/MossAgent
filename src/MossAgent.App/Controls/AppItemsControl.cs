using Avalonia.Controls;

namespace MossAgent.App.Controls;

public sealed class AppItemsControl : ItemsControl
{
    protected override Type StyleKeyOverride => typeof(ItemsControl);
}
