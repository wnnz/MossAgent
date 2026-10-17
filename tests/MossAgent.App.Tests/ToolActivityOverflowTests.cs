using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ToolActivityOverflowTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    public static IEnumerable<object[]> Cases()
    {
        foreach (var dark in new[] { false, true })
            foreach (var expandable in new[] { false, true })
                foreach (var width in new[] { 280, 740 })
                    foreach (var longStatus in new[] { false, true })
                        yield return [dark, expandable, width, longStatus];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public Task LongHeaders_PreserveInsetsAndReadableSummary(bool dark, bool expandable, int width, bool longStatus) => _session.Dispatch(() =>
    {
        var invoked = 0;
        var row = new AppToolActivityRow
        {
            Summary = new string('文', 160),
            Status = longStatus ? new string('态', 80) : "等待用户批准",
            CanExpand = expandable,
            Command = new RelayCommand(() => invoked++),
            Margin = new Thickness(12),
            VerticalAlignment = VerticalAlignment.Top
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
            var texts = row.GetVisualDescendants().OfType<AppText>().Where(text => text.IsEffectivelyVisible).ToArray();
            var summary = Assert.Single(texts, text => text.Text == row.Summary);
            var status = Assert.Single(texts, text => text.Text == row.Status);
            var summaryOrigin = summary.TranslatePoint(default, row)!.Value;
            var statusOrigin = status.TranslatePoint(default, row)!.Value;
            Assert.InRange(summary.Bounds.Width, 64, width);
            Assert.InRange(status.Bounds.Width, 1, 120);
            Assert.Equal(6, row.Bounds.Width - statusOrigin.X - status.Bounds.Width);
            Assert.InRange(statusOrigin.X - summaryOrigin.X - summary.Bounds.Width, 6, 8);
            Assert.Equal(32, row.Bounds.Height);
            Assert.Equal(Avalonia.Media.TextTrimming.CharacterEllipsis, status.TextTrimming);
            Assert.Equal(row.Status, ToolTip.GetTip(status));
            Assert.Equal(row.Status, AutomationProperties.GetHelpText(row.FindControl<AppButton>("ToolButton")!));
            Assert.Equal(0, invoked);
            row.Status = "完成";
            window.UpdateLayout();
            Assert.Equal("完成", status.Text);
            Assert.Equal("完成", ToolTip.GetTip(status));
            Assert.Equal(0, invoked);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
