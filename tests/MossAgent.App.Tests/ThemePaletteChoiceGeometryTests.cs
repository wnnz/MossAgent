using Avalonia;
using Avalonia.Automation;
using Avalonia.Headless;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ThemePaletteChoiceGeometryTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 384)]
    [InlineData(true, 384)]
    [InlineData(false, 728)]
    [InlineData(true, 728)]
    public Task DefaultChoicesMatchMeasuredPillsAndContainTheAccentSwatch(bool dark, double width) =>
        _session.Dispatch(() =>
        {
            using var fixture = new ThemePaletteChoiceFixture(dark, width);
            var selectors = fixture.Card.GetVisualDescendants().OfType<AppComboBox>().ToArray();
            var accent = Assert.Single(selectors, selector => AutomationProperties.GetName(selector) == "主题强调色");
            var font = Assert.Single(selectors, selector => AutomationProperties.GetName(selector) == "主题界面字体");
            Assert.Equal(new Size(78, 28), accent.Bounds.Size);
            Assert.Equal(new Size(60, 28), font.Bounds.Size);
            var swatch = Assert.Single(accent.GetVisualDescendants().OfType<AppColorSwatch>());
            Assert.Equal(new Size(14, 14), swatch.Bounds.Size);
            Assert.Equal(fixture.Editor.AccentHex, swatch.ColorHex);
            var swatchOrigin = swatch.TranslatePoint(default, accent)!.Value;
            Assert.Equal(8, swatchOrigin.X);
            Assert.Equal(7, swatchOrigin.Y);
            Assert.All(selectors, selector =>
                Assert.InRange(selector.TranslatePoint(default, fixture.Card)!.Value.X + selector.Bounds.Width,
                    width - 18, width - 16));
            Assert.Contains(font.GetVisualDescendants().OfType<AppText>(), text => text.Text == "系统");
            Assert.Contains(fixture.Editor.Fonts.UiFontOptions, option => option.Label == "系统默认");
        }, TestContext.Current.CancellationToken);
}
