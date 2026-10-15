using Avalonia;
using Avalonia.Media;

namespace MossAgent.App.Controls;

public sealed partial class AppImagePreview : AppUserControl
{
    public static readonly StyledProperty<IImage?> SourceProperty = AvaloniaProperty.Register<AppImagePreview, IImage?>(nameof(Source));
    public static readonly StyledProperty<bool> IsFitProperty = AvaloniaProperty.Register<AppImagePreview, bool>(nameof(IsFit), true);
    public static readonly StyledProperty<double> ScaleProperty = AvaloniaProperty.Register<AppImagePreview, double>(nameof(Scale), 1);
    public AppImagePreview() { InitializeComponent(); Classes.Add("app-image-preview"); }
    public IImage? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }
    public bool IsFit { get => GetValue(IsFitProperty); set => SetValue(IsFitProperty, value); }
    public double Scale { get => GetValue(ScaleProperty); set => SetValue(ScaleProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (ImageSurface is null) return;
        if (change.Property == SourceProperty || change.Property == IsFitProperty || change.Property == ScaleProperty)
        {
            Scroller.Offset = default;
            UpdateImageSize();
        }
        else if (change.Property == BoundsProperty) UpdateImageSize();
    }

    private void UpdateImageSize()
    {
        if (Source is not { Size.Width: > 0, Size.Height: > 0 } source)
        { ImageSurface.Width = 0; ImageSurface.Height = 0; return; }
        var width = Math.Max(0, Bounds.Width - Scroller.Padding.Left - Scroller.Padding.Right);
        var height = Math.Max(0, Bounds.Height - Scroller.Padding.Top - Scroller.Padding.Bottom);
        var scale = IsFit ? Math.Min(1, Math.Min(width / source.Size.Width, height / source.Size.Height))
            : double.IsFinite(Scale) ? Math.Clamp(Scale, .1, 4) : 1;
        ImageSurface.Width = source.Size.Width * scale;
        ImageSurface.Height = source.Size.Height * scale;
    }
}
