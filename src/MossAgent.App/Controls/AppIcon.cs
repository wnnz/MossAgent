using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaPath = Avalonia.Controls.Shapes.Path;
using AvaloniaShape = Avalonia.Controls.Shapes.Shape;

namespace MossAgent.App.Controls;

public sealed class AppIcon : Control
{
    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<AppIcon, double>(nameof(Size), 14);

    public static readonly StyledProperty<Geometry?> DataProperty = AvaloniaPath.DataProperty.AddOwner<AppIcon>();
    public static readonly StyledProperty<Drawing?> DrawingProperty =
        AvaloniaProperty.Register<AppIcon, Drawing?>(nameof(Drawing));
    public static readonly StyledProperty<IBrush?> FillProperty = AvaloniaShape.FillProperty.AddOwner<AppIcon>();
    public static readonly StyledProperty<IBrush?> StrokeProperty = AvaloniaShape.StrokeProperty.AddOwner<AppIcon>();
    public static readonly StyledProperty<double> StrokeThicknessProperty = AvaloniaShape.StrokeThicknessProperty.AddOwner<AppIcon>();
    public static readonly StyledProperty<PenLineCap> StrokeLineCapProperty = AvaloniaShape.StrokeLineCapProperty.AddOwner<AppIcon>();
    public static readonly StyledProperty<PenLineJoin> StrokeJoinProperty = AvaloniaShape.StrokeJoinProperty.AddOwner<AppIcon>();
    public static readonly StyledProperty<double> StrokeMiterLimitProperty = AvaloniaShape.StrokeMiterLimitProperty.AddOwner<AppIcon>();
    public static readonly StyledProperty<AvaloniaList<double>?> StrokeDashArrayProperty = AvaloniaShape.StrokeDashArrayProperty.AddOwner<AppIcon>();
    public static readonly StyledProperty<double> StrokeDashOffsetProperty = AvaloniaShape.StrokeDashOffsetProperty.AddOwner<AppIcon>();

    static AppIcon() => AffectsRender<AppIcon>(DataProperty, DrawingProperty, FillProperty, StrokeProperty, StrokeThicknessProperty,
        StrokeLineCapProperty, StrokeJoinProperty, StrokeMiterLimitProperty, StrokeDashArrayProperty, StrokeDashOffsetProperty);

    public Geometry? Data { get => GetValue(DataProperty); set => SetValue(DataProperty, value); }
    public Drawing? Drawing { get => GetValue(DrawingProperty); set => SetValue(DrawingProperty, value); }
    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public IBrush? Stroke { get => GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
    public double StrokeThickness { get => GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }
    public PenLineCap StrokeLineCap { get => GetValue(StrokeLineCapProperty); set => SetValue(StrokeLineCapProperty, value); }
    public PenLineJoin StrokeJoin { get => GetValue(StrokeJoinProperty); set => SetValue(StrokeJoinProperty, value); }
    public double StrokeMiterLimit { get => GetValue(StrokeMiterLimitProperty); set => SetValue(StrokeMiterLimitProperty, value); }
    public AvaloniaList<double>? StrokeDashArray { get => GetValue(StrokeDashArrayProperty); set => SetValue(StrokeDashArrayProperty, value); }
    public double StrokeDashOffset { get => GetValue(StrokeDashOffsetProperty); set => SetValue(StrokeDashOffsetProperty, value); }

    public AppIcon()
    {
        Width = Size;
        Height = Size;
    }

    public double Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        if (Drawing is { } drawing)
        {
            if (AppIconGeometryLayout.Arrange(drawing.GetBounds(), Bounds.Size) is not { } drawingLayout) return;
            using (context.PushTransform(drawingLayout.Transform)) drawing.Draw(context);
            return;
        }
        if (Data is null || AppIconGeometryLayout.Arrange(Data.Bounds, Bounds.Size) is not { } layout) return;
        var pen = new Pen
        {
            Brush = Stroke,
            Thickness = StrokeThickness / layout.Scale,
            LineCap = StrokeLineCap,
            LineJoin = StrokeJoin,
            MiterLimit = StrokeMiterLimit,
            DashStyle = StrokeDashArray is null ? null : new DashStyle(StrokeDashArray, StrokeDashOffset)
        };
        using (context.PushTransform(layout.Transform)) context.DrawGeometry(Fill, pen, Data);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != SizeProperty) return;
        Width = Size;
        Height = Size;
    }
}
