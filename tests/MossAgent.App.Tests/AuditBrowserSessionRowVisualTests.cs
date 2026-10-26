using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class AuditBrowserSessionRowVisualTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(220, false, "running")]
    [InlineData(360, true, "success")]
    [InlineData(220, true, "interrupted")]
    [InlineData(360, false, "failed")]
    public Task SharedRowAlignsIconsTextAndBounds(int width, bool dark, string kind) => _session.Dispatch(() =>
    {
        var intent = new BrowserActionIntentRecord(Guid.NewGuid(), Guid.NewGuid(),
            BrowserActionKind.Navigate, "a1b2c3d4e5f6a1b2", BrowserActionIntentState.Interrupted,
            DateTimeOffset.UtcNow.AddSeconds(-1), DateTimeOffset.UtcNow);
        var row = new AppAuditBrowserSessionRow
        {
            Title = "内置浏览器 · 隔离资料",
            OutcomeKind = kind,
            OutcomeLabel = "已中断",
            Metadata = "2026/10/09 10:00:00 · 耗时 240 毫秒",
            Detail = "应用关闭前该浏览器会话仍处于活动状态，实际页面结果未知；请核实状态后手动继续。",
            Actions = new[] { new AuditBrowserActionViewModel(intent) }
        };
        var window = new AppWindow
        {
            Content = row,
            Width = width,
            Height = 200,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            var icon = row.FindControl<AppIcon>("OutcomeIcon")!;
            var title = row.FindControl<AppText>("SessionTitle")!;
            var outcome = row.FindControl<AppText>("OutcomeText")!;
            Assert.Equal(14, icon.Size);
            Assert.NotNull(icon.Data);
            var iconCenter = icon.TranslatePoint(new Point(7, 7), row)!.Value.Y;
            Assert.InRange(Math.Abs(title.TranslatePoint(new Point(0, title.Bounds.Height / 2), row)!.Value.Y - iconCenter), 0, 0.5);
            Assert.InRange(Math.Abs(outcome.TranslatePoint(new Point(0, outcome.Bounds.Height / 2), row)!.Value.Y - iconCenter), 0, 0.5);
            Assert.All(row.GetVisualDescendants().OfType<AppText>(), text =>
                Assert.True(text.TranslatePoint(new Point(text.Bounds.Width, 0), row)!.Value.X <= row.Bounds.Width + 1));
            Assert.Contains(kind, row.Classes);
            Assert.True(row.HasDetail);
            Assert.True(row.HasActions);
            Assert.True(row.FindControl<AppText>("DetailText")!.IsEffectivelyVisible);
            Assert.True(row.FindControl<AppItemsControl>("ActionList")!.IsEffectivelyVisible);
            row.Detail = string.Empty;
            row.Actions = null;
            window.UpdateLayout();
            Assert.False(row.FindControl<AppText>("DetailText")!.IsEffectivelyVisible);
            Assert.False(row.FindControl<AppItemsControl>("ActionList")!.IsEffectivelyVisible);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task UnknownOutcomeKindFallsBackToTheFailureTier() => _session.Dispatch(() =>
    {
        var row = new AppAuditBrowserSessionRow { OutcomeKind = "unknown" };
        Assert.Contains("failed", row.Classes);
        Assert.DoesNotContain("running", row.Classes);
        row.OutcomeKind = "running";
        Assert.Contains("running", row.Classes);
        Assert.DoesNotContain("failed", row.Classes);
    }, TestContext.Current.CancellationToken);
}
