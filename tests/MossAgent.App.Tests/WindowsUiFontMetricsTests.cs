using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class WindowsUiFontMetricsTests
{
    [Fact]
    public async Task SharedLayoutHostUsesProportionalWindowsFontMetrics()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
        await session.Dispatch(() =>
        {
            using var wide = new TextLayout("WWW", new Typeface("Segoe UI"), 13, Brushes.Black);
            using var narrow = new TextLayout("iii", new Typeface("Segoe UI"), 13, Brushes.Black);

            Assert.True(wide.Width > narrow.Width * 2,
                $"System font metrics were substituted: WWW={wide.Width}, iii={narrow.Width}");
        }, TestContext.Current.CancellationToken);
    }
}
