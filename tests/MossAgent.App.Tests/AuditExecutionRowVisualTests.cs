using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class AuditExecutionRowVisualTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(220, false, "pending")]
    [InlineData(360, false, "success")]
    [InlineData(220, true, "failed")]
    [InlineData(360, true, "denied")]
    public Task SharedRowAlignsIconsTextAndBounds(int width, bool dark, string kind) => _session.Dispatch(() =>
    {
        var row = new AppAuditExecutionRow
        {
            ToolName = new string('x', 160),
            OutcomeKind = kind,
            OutcomeLabel = "进行中",
            Metadata = "2026/10/09 10:00:00 · 耗时 121 毫秒",
            Detail = "请检查项目的授权目录后重试。"
        };
        var window = new AppWindow
        {
            Content = row,
            Width = width,
            Height = 180,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            var icon = row.FindControl<AppIcon>("OutcomeIcon")!;
            var title = row.FindControl<AppText>("ToolTitle")!;
            var outcome = row.FindControl<AppText>("OutcomeText")!;
            Assert.Equal(14, icon.Size);
            Assert.NotNull(icon.Data);
            var iconCenter = icon.TranslatePoint(new Point(7, 7), row)!.Value.Y;
            Assert.InRange(Math.Abs(title.TranslatePoint(new Point(0, title.Bounds.Height / 2), row)!.Value.Y - iconCenter), 0, 0.5);
            Assert.InRange(Math.Abs(outcome.TranslatePoint(new Point(0, outcome.Bounds.Height / 2), row)!.Value.Y - iconCenter), 0, 0.5);
            Assert.All(row.GetVisualDescendants().OfType<AppText>(), text =>
                Assert.True(text.TranslatePoint(new Point(text.Bounds.Width, 0), row)!.Value.X <= row.Bounds.Width));
            Assert.Contains(kind, row.Classes);
            row.Detail = string.Empty;
            window.UpdateLayout();
            Assert.False(row.FindControl<AppText>("DetailText")!.IsEffectivelyVisible);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(220, false)]
    [InlineData(360, true)]
    public Task SavedReasoningFailureWrapsWithoutClipping(int width, bool dark) => _session.Dispatch(() =>
    {
        var row = CreateReasoningFailureRow();
        var window = new AppWindow
        {
            Content = row,
            Width = width,
            Height = 220,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            var detail = row.FindControl<AppText>("DetailText")!;
            Assert.Equal(row.Detail, detail.Text);
            Assert.Contains("子代理设置", detail.Text);
            Assert.DoesNotContain("权限", detail.Text);
            Assert.True(detail.IsEffectivelyVisible);
            Assert.Equal(TextWrapping.Wrap, detail.TextWrapping);
            Assert.True(detail.Bounds.Height > 18);
            var bottomRight = detail.TranslatePoint(new Point(detail.Bounds.Width, detail.Bounds.Height), row)!.Value;
            Assert.InRange(bottomRight.X, 0, row.Bounds.Width);
            Assert.InRange(bottomRight.Y, 0, row.Bounds.Height);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static AppAuditExecutionRow CreateReasoningFailureRow()
    {
        var started = DateTimeOffset.UnixEpoch;
        var record = new ToolExecutionAuditRecord(Guid.NewGuid(), Guid.NewGuid(), "child-call", "subagent.run",
            ToolRiskLevel.ExternalSideEffect, ApprovalPolicy.FullAccess, started, started.AddSeconds(1),
            true, false, "subagent_reasoning_unavailable", 64, 0);
        var presentation = new AuditExecutionViewModel(record);
        return new AppAuditExecutionRow
        {
            ToolName = presentation.ToolName,
            OutcomeKind = presentation.OutcomeKind,
            OutcomeLabel = presentation.OutcomeLabel,
            Metadata = presentation.Metadata,
            Detail = presentation.Detail
        };
    }
}
