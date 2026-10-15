using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace MossAgent.App.Controls;

public sealed class AppSlider : Slider
{
    public AppSlider()
    {
        Classes.Add("app-slider");
        Resources["SliderPreContentMargin"] = new GridLength(0);
        Resources["SliderPostContentMargin"] = new GridLength(0);
        Resources["SliderHorizontalHeight"] = 24d;
        Resources["SliderTrackThemeHeight"] = 24d;
        Resources["ControlCornerRadius"] = new CornerRadius(12);
        Resources["SliderThumbBackground"] = Brushes.WhiteSmoke;
        Resources["SliderThumbBackgroundPointerOver"] = Brushes.WhiteSmoke;
        Resources["SliderThumbBackgroundPressed"] = Brushes.WhiteSmoke;
    }

    protected override Type StyleKeyOverride => typeof(Slider);
}
