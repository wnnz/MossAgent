using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class InterruptedAuditRowTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(220, false)]
    [InlineData(220, true)]
    [InlineData(360, false)]
    [InlineData(360, true)]
    public Task RecoveredUnknownOutcomeUsesTheSharedRowAndWrapsItsRecoveryHint(int width, bool dark) => _session.Dispatch(() =>
    {
        var record = new ToolExecutionAuditRecord(Guid.NewGuid(), Guid.NewGuid(), "call", "browser.navigate",
            ToolRiskLevel.ExternalSideEffect, ApprovalPolicy.AskEveryTime, DateTimeOffset.UnixEpoch,
            null, null, null, null, null, 0, DateTimeOffset.UnixEpoch.AddSeconds(1));
        var item = new AuditExecutionViewModel(record);
        var row = new AppAuditExecutionRow
        {
            ToolName = item.ToolName, OutcomeKind = item.OutcomeKind, OutcomeLabel = item.OutcomeLabel,
            Detail = item.Detail, Metadata = item.Metadata
        };
        var window = new AppWindow { Content = row, Width = width, Height = 240,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            window.UpdateLayout();
            Assert.Contains("interrupted", row.Classes);
            Assert.DoesNotContain("failed", row.Classes);
            Assert.DoesNotContain("pending", row.Classes);
            Assert.Equal("已中断", row.FindControl<AppText>("OutcomeText")!.Text);
            var detail = row.FindControl<AppText>("DetailText")!;
            Assert.Equal(item.Detail, detail.Text);
            Assert.True(detail.IsEffectivelyVisible);
            Assert.Equal(TextWrapping.Wrap, detail.TextWrapping);
            Assert.True(detail.Bounds.Height > 18);
            var bottomRight = detail.TranslatePoint(new Point(detail.Bounds.Width, detail.Bounds.Height), row)!.Value;
            Assert.InRange(bottomRight.X, 0, row.Bounds.Width);
            Assert.InRange(bottomRight.Y, 0, row.Bounds.Height);
            Assert.NotNull(row.FindControl<AppIcon>("OutcomeIcon")!.Data);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
