using Avalonia;
using Avalonia.Markup.Xaml;

namespace MossAgent.App.Controls;

public sealed partial class AppTaskFailureNotice : AppUserControl
{
    public static readonly StyledProperty<string> HintProperty =
        AvaloniaProperty.Register<AppTaskFailureNotice, string>(nameof(Hint), string.Empty);

    public AppTaskFailureNotice() => AvaloniaXamlLoader.Load(this);

    public string Hint
    {
        get => GetValue(HintProperty);
        set => SetValue(HintProperty, value);
    }
}
