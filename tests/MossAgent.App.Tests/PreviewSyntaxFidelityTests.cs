using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class PreviewSyntaxFidelityTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task PreviewTokensUseTheirOwnPaletteWithoutChangingInspectorColors(bool dark) => _session.Dispatch(() =>
    {
        var preview = new AppDiffPreview();
        var window = new AppWindow
        {
            Width = 768,
            Height = 200,
            Content = preview,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            AssertPalette(preview, dark);
            var theme = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            AssertInspectorBrush("CodeKeywordBrush", theme, dark ? "#C586C0" : "#AF00DB");
            AssertInspectorBrush("CodeTypeBrush", theme, dark ? "#4EC9B0" : "#267F99");
            window.RequestedThemeVariant = dark ? ThemeVariant.Light : ThemeVariant.Dark;
            window.UpdateLayout();
            AssertPalette(preview, !dark);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static void AssertPalette(AppDiffPreview preview, bool dark)
    {
        (string Variant, string Light, string Dark)[] colors =
        [
            ("keyword", "#751ED9", "#C586C0"),
            ("identifier", "#BD5800", "#9CDCFE"),
            ("type", "#751ED9", "#4EC9B0"),
            ("property", "#BD5800", "#D7BA7D"),
            ("string", "#008809", "#A5D6A7"),
            ("number", "#0071EA", "#B5CEA8")
        ];
        foreach (var (variant, light, night) in colors)
        {
            var tokens = preview.GetVisualDescendants().OfType<AppText>()
                .Where(text => text.Classes.Contains("diff-token-" + variant)).ToArray();
            Assert.NotEmpty(tokens);
            Assert.All(tokens, token => Assert.Equal(Color.Parse(dark ? night : light),
                Assert.IsAssignableFrom<ISolidColorBrush>(token.Foreground).Color));
        }
    }

    private static void AssertInspectorBrush(string key, ThemeVariant theme, string color)
    {
        Assert.True(Avalonia.Application.Current!.Resources.TryGetResource(key, theme, out var value));
        Assert.Equal(Color.Parse(color), Assert.IsAssignableFrom<ISolidColorBrush>(value).Color);
    }
}
