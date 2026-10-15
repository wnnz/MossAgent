using Avalonia;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class AppIconGeometryLayoutTests
{
    [Theory]
    [InlineData(4, 6, 16, 12, 14)]
    [InlineData(4, 11, 16, 2, 9)]
    [InlineData(4, 12, 16, 0, 9)]
    [InlineData(12, 4, 0, 16, 14)]
    [InlineData(-10, -6, 20, 12, 14)]
    [InlineData(3, 3, 18, 18, 46)]
    public void EveryGeometry_IsUniformlyScaledAndCentered(double left, double top, double width, double height, double size)
    {
        var bounds = new Rect(left, top, width, height);
        var layout = AppIconGeometryLayout.Arrange(bounds, new Size(size, size))!.Value;
        var center = layout.Transform.Transform(bounds.Center);
        var first = layout.Transform.Transform(bounds.TopLeft);
        var last = layout.Transform.Transform(bounds.BottomRight);

        Assert.Equal(size / 2, center.X, precision: 8);
        Assert.Equal(size / 2, center.Y, precision: 8);
        Assert.InRange(first.X, -0.000001, size);
        Assert.InRange(first.Y, -0.000001, size);
        Assert.InRange(last.X, 0, size + 0.000001);
        Assert.InRange(last.Y, 0, size + 0.000001);
        Assert.Equal(width * layout.Scale, last.X - first.X, precision: 8);
        Assert.Equal(height * layout.Scale, last.Y - first.Y, precision: 8);
    }

    [Theory]
    [InlineData(0, 0, 14, 14)]
    [InlineData(16, 12, 0, 0)]
    [InlineData(16, 12, -1, 14)]
    [InlineData(16, 0, 14, 0)]
    [InlineData(0, 16, 0, 14)]
    [InlineData(16, 12, double.NaN, 14)]
    [InlineData(16, 12, 14, double.PositiveInfinity)]
    [InlineData(double.NaN, 12, 14, 14)]
    [InlineData(16, double.PositiveInfinity, 14, 14)]
    public void EmptyGeometryOrUnavailableViewport_DoesNotProduceAnInvalidTransform(
        double width, double height, double viewportWidth, double viewportHeight)
    {
        Assert.Null(AppIconGeometryLayout.Arrange(new Rect(4, 4, width, height),
            new Size(viewportWidth, viewportHeight)));
    }
}
