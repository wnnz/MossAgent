using Avalonia;

namespace MossAgent.App.Controls;

internal static class AppIconGeometryLayout
{
    public static (double Scale, Matrix Transform)? Arrange(Rect geometry, Size viewport)
    {
        if (!double.IsFinite(viewport.Width) || !double.IsFinite(viewport.Height)
            || viewport.Width <= 0 || viewport.Height <= 0
            || !double.IsFinite(geometry.X) || !double.IsFinite(geometry.Y)
            || !double.IsFinite(geometry.Width) || !double.IsFinite(geometry.Height)
            || geometry.Width < 0 || geometry.Height < 0) return null;
        var scale = Math.Min(geometry.Width > 0 ? viewport.Width / geometry.Width : double.PositiveInfinity,
            geometry.Height > 0 ? viewport.Height / geometry.Height : double.PositiveInfinity);
        if (!double.IsFinite(scale) || scale <= 0) return null;
        var offset = new Vector((viewport.Width - geometry.Width * scale) / 2,
            (viewport.Height - geometry.Height * scale) / 2);
        var transform = Matrix.CreateTranslation(-geometry.X, -geometry.Y)
            * Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offset);
        return (scale, transform);
    }
}
