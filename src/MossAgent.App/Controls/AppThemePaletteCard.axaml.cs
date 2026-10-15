using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;

namespace MossAgent.App.Controls;

[PseudoClasses(":compact")]
public sealed partial class AppThemePaletteCard : AppUserControl
{
    public AppThemePaletteCard() => InitializeComponent();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty) PseudoClasses.Set(":compact", Bounds.Width < 352);
    }
}
