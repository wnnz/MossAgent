using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;

namespace MossAgent.App.Controls;

[PseudoClasses(":overflowing")]
public sealed class AppText : TextBlock
{
    public static readonly StyledProperty<bool> FadeOverflowProperty =
        AvaloniaProperty.Register<AppText, bool>(nameof(FadeOverflow));

    public bool FadeOverflow
    {
        get => GetValue(FadeOverflowProperty);
        set => SetValue(FadeOverflowProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FadeOverflowProperty) InvalidateArrange();
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var arranged = base.ArrangeOverride(finalSize);
        PseudoClasses.Set(":overflowing", FadeOverflow && TextWrapping == Avalonia.Media.TextWrapping.NoWrap
            && TextLayout.Width > Math.Max(0, finalSize.Width - Padding.Left - Padding.Right));
        return arranged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs args)
    {
        PseudoClasses.Set(":overflowing", false);
        base.OnDetachedFromVisualTree(args);
    }
}
