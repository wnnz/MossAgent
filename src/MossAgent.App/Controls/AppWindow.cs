using Avalonia.Controls;
using Avalonia.Media;

namespace MossAgent.App.Controls;

public class AppWindow : Window
{
    public AppWindow()
    {
        Classes.Add("app-window");
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.Antialias);
    }

    protected override Type StyleKeyOverride => typeof(Window);
}
