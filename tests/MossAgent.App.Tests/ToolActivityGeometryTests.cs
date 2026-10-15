using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ToolActivityGeometryTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    public static IEnumerable<object[]> Cases()
    {
        foreach (var width in new[] { 280, 740 })
            foreach (var dark in new[] { false, true })
                foreach (var canExpand in new[] { false, true })
                    foreach (var subagent in new[] { false, true })
                        yield return [width, dark, canExpand, subagent];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public Task Header_UsesCompactAlignedGlyphsWithoutAnEmptyStaticDisclosureGutter(
        int width, bool dark, bool canExpand, bool subagent) => _session.Dispatch(() =>
    {
        var invoked = 0;
        var row = new AppToolActivityRow
        {
            Summary = "运行合成工具",
            Status = "完成",
            CanExpand = canExpand,
            IsSubagent = subagent,
            Command = new RelayCommand(() => invoked++),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
        };
        var window = new Window
        {
            Content = row,
            Width = width,
            Height = 80,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            VerifyGeometry(row, width, canExpand);
            Assert.Equal(0, invoked);
            var button = row.FindControl<AppButton>("ToolButton")!;
            Assert.Equal(canExpand, button.IsEffectivelyVisible);
            if (canExpand)
            {
                button.Focus();
                window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
                window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
                Assert.Equal(1, invoked);
            }
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static void VerifyGeometry(AppToolActivityRow row, int width, bool canExpand)
    {
        var glyph = Assert.Single(row.GetVisualDescendants().OfType<AppIcon>(),
            icon => icon.IsEffectivelyVisible && !icon.Classes.Contains("tool-expander"));
        var summary = Assert.Single(row.GetVisualDescendants().OfType<AppText>(),
            text => text.IsEffectivelyVisible && text.Text == row.Summary);
        var status = Assert.Single(row.GetVisualDescendants().OfType<AppText>(),
            text => text.IsEffectivelyVisible && text.Text == row.Status);
        Assert.Equal(12, glyph.Bounds.Width);
        Assert.Equal(12, glyph.Bounds.Height);
        if (!row.IsSubagent) Assert.Equal(glyph.Data!.Bounds.Width, glyph.Data.Bounds.Height);
        Assert.Equal(32, row.Bounds.Height);
        Assert.InRange(row.Bounds.Width, 1, width);
        Assert.Equal(13, summary.FontSize);
        Assert.Equal(10, status.FontSize);
        var glyphOrigin = glyph.TranslatePoint(default, row)!.Value;
        var summaryOrigin = summary.TranslatePoint(default, row)!.Value;
        var statusOrigin = status.TranslatePoint(default, row)!.Value;
        Assert.Equal(canExpand ? 43 : 21, summaryOrigin.X);
        Assert.InRange(Math.Abs(glyphOrigin.Y + glyph.Bounds.Height / 2
            - summaryOrigin.Y - summary.Bounds.Height / 2), 0, 0.5);
        Assert.InRange(Math.Abs(glyphOrigin.Y + glyph.Bounds.Height / 2
            - statusOrigin.Y - status.Bounds.Height / 2), 0, 0.5);
    }
}
