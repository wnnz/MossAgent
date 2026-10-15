using Avalonia.Controls;

namespace MossAgent.App.Controls;

public sealed class AppContentControl : ContentControl
{
    protected override Type StyleKeyOverride => typeof(ContentControl);
}
