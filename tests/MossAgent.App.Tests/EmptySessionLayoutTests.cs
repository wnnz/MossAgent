using Avalonia;
using Avalonia.Headless;
using Avalonia.Styling;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class EmptySessionLayoutTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Resize_ContainsBrandAndHeadingWhilePreservingTheWidePosition(bool dark) => _session.Dispatch(() =>
    {
        var brand = new AppBorder { Width = 46, Height = 46 };
        var heading = new AppBorder { Width = 200, Height = 74 };
        var layout = new AppEmptySessionLayout { Children = { brand, heading } };
        var window = new AppWindow
        {
            Width = 500,
            Height = 600,
            Content = layout,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            Assert.Equal(32, layout.BrandOffset);
            foreach (var height in new[] { 600, 141, 152, 184, 600 })
            {
                layout.Height = height;
                window.UpdateLayout();
                Assert.InRange(brand.Bounds.Top, 0, height);
                Assert.True(brand.Bounds.Bottom <= heading.Bounds.Top);
                Assert.True(heading.Bounds.Bottom <= layout.Bounds.Height);
                Assert.Null(brand.RenderTransform);
                if (height >= 184)
                {
                    Assert.Equal((height - 120) / 2d - 32, brand.Bounds.Top);
                    Assert.Equal((height - 120) / 2d + 46, heading.Bounds.Top);
                }
            }
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public void BrandOffset_RejectsInvalidDimensions()
    {
        var layout = new AppEmptySessionLayout();
        Assert.Throws<ArgumentException>(() => layout.BrandOffset = -1);
        Assert.Throws<ArgumentException>(() => layout.BrandOffset = double.NaN);
        Assert.Throws<ArgumentException>(() => layout.BrandOffset = double.PositiveInfinity);
    }
}
