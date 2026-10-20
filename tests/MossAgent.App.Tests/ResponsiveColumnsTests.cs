using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ResponsiveColumnsTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 470, true)]
    [InlineData(true, 470, true)]
    [InlineData(false, 579, true)]
    [InlineData(true, 579, true)]
    [InlineData(false, 580, false)]
    [InlineData(true, 580, false)]
    [InlineData(false, 900, false)]
    [InlineData(true, 900, false)]
    public Task CompactColumnsStackWithoutRecreatingTheirContents(bool dark, double width, bool compact) => _session.Dispatch(() =>
    {
        var primary = new AppInput { Text = "Retained" };
        var secondary = new AppButton { Content = "History" };
        var columns = new AppResponsiveColumns { Children = { primary, secondary } };
        var window = new AppWindow { Width = width, Height = 500, Content = columns, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            window.UpdateLayout();
            Assert.Equal(compact, columns.IsCompact);
            if (compact)
            {
                Assert.Equal(width, primary.Bounds.Width);
                Assert.Equal(width, secondary.Bounds.Width);
                Assert.Equal(primary.Bounds.Bottom + columns.Gap, secondary.Bounds.Top);
            }
            else
            {
                Assert.Equal(columns.SecondaryWidth, secondary.Bounds.Width);
                Assert.Equal(primary.Bounds.Right + columns.Gap, secondary.Bounds.Left);
            }
            window.Width = compact ? 900 : 470;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Assert.NotEqual(compact, columns.IsCompact);
            Assert.Same(primary, columns.Children[0]);
            Assert.Same(secondary, columns.Children[1]);
            Assert.Equal("Retained", primary.Text);
            Assert.True(primary.Bounds.Width > 0 && secondary.Bounds.Height > 0);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
    [Theory]
    [InlineData(470, true)]
    [InlineData(900, false)]
    public Task FixedPrimaryLeavesRemainingWidthForReviewContent(double width, bool compact) => _session.Dispatch(() =>
    {
        var list = new AppInput { Text = "PR list" };
        var detail = new AppButton { Content = "Review content" };
        var columns = new AppResponsiveColumns { PrimaryWidth = 260, Children = { list, detail } };
        var window = new AppWindow { Content = columns, Width = width, Height = 500 };
        window.Show();
        try
        {
            window.UpdateLayout();
            Assert.Equal(compact, columns.IsCompact);
            Assert.Equal(compact ? width : columns.PrimaryWidth, list.Bounds.Width);
            Assert.Equal(compact ? width : width - columns.PrimaryWidth - columns.Gap, detail.Bounds.Width);
            columns.PrimaryWidth = double.NaN;
            window.UpdateLayout();
            Assert.Equal(compact ? width : columns.SecondaryWidth, detail.Bounds.Width);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task CompactHiddenPaneDoesNotReserveHalfTheHeight(bool hidePrimary) => _session.Dispatch(() =>
    {
        var primary = new AppButton { Content = "Primary", IsVisible = !hidePrimary };
        var secondary = new AppButton { Content = "Secondary", IsVisible = hidePrimary };
        var columns = new AppResponsiveColumns { Children = { primary, secondary } };
        var window = new AppWindow { Content = columns, Width = 470, Height = 500 };
        window.Show();
        try
        {
            window.UpdateLayout();
            Assert.True(columns.IsCompact); Assert.Equal(0, columns.RowSpacing);
            Assert.Equal(columns.Bounds.Height, columns.RowDefinitions[hidePrimary ? 1 : 0].ActualHeight);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
