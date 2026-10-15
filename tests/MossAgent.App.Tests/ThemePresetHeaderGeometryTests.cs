using Avalonia;
using Avalonia.Automation;
using Avalonia.Headless;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ThemePresetHeaderGeometryTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 320)]
    [InlineData(true, 320)]
    [InlineData(false, 728)]
    [InlineData(true, 728)]
    public Task HeaderUsesAlignedSharedPillAndIconHitAreasWithoutClipping(bool dark, double width) =>
        _session.Dispatch(() =>
        {
            using var fixture = new ThemePaletteChoiceFixture(dark, width);
            var selector = Assert.Single(fixture.Card.GetVisualDescendants().OfType<AppPillComboBox>(),
                control => AutomationProperties.GetName(control) == "主题配色");
            var selectorWidth = width < 352 ? 144 : 176;
            Assert.Equal(selectorWidth, selector.Bounds.Width);
            Assert.Equal(28, selector.Bounds.Height);
            var mark = Assert.Single(selector.GetVisualDescendants().OfType<AppBorder>(),
                control => control.Classes.Contains("theme-preset-mark"));
            Assert.Equal(new Size(20, 20), mark.Bounds.Size);
            Assert.Equal(new CornerRadius(10), mark.CornerRadius);
            var center = selector.TranslatePoint(new Point(0, selector.Bounds.Height / 2), fixture.Card)!.Value.Y;
            foreach (var button in fixture.Card.GetVisualDescendants().OfType<AppIconButton>())
            {
                Assert.Equal(new Size(32, 32), button.Bounds.Size);
                Assert.Equal(center, button.TranslatePoint(new Point(0, 16), fixture.Card)!.Value.Y);
                Assert.True(button.TranslatePoint(new Point(32, 0), fixture.Card)!.Value.X
                    <= selector.TranslatePoint(default, fixture.Card)!.Value.X - 8);
            }
            Assert.True(selector.TranslatePoint(new Point(selectorWidth, 0), fixture.Card)!.Value.X <= fixture.Card.Bounds.Width);
            var label = Assert.Single(fixture.Card.GetVisualDescendants().OfType<AppText>(), text => text.Text == "主题");
            Assert.True(label.TranslatePoint(new Point(label.Bounds.Width, 0), fixture.Card)!.Value.X
                <= fixture.Card.GetVisualDescendants().OfType<AppIconButton>().First()
                    .TranslatePoint(default, fixture.Card)!.Value.X - 8);
        }, TestContext.Current.CancellationToken);
}
