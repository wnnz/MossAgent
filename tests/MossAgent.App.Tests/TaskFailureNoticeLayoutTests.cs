using Avalonia;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class TaskFailureNoticeLayoutTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 320)]
    [InlineData(true, 320)]
    [InlineData(false, 640)]
    [InlineData(true, 640)]
    public Task FailureNotice_WrapsInsideAvailableWidthAndUpdatesHint(bool dark, int width) => _session.Dispatch(() =>
    {
        var hint = WorkspaceFailureText.Get(TaskFailureReason.StreamLineLimit);
        var notice = new AppTaskFailureNotice { Hint = hint + " " + hint };
        var window = new AppWindow
        {
            Width = width,
            Height = 240,
            Content = notice,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            var text = Assert.Single(notice.GetVisualDescendants().OfType<AppText>());
            var icon = Assert.Single(notice.GetVisualDescendants().OfType<AppIcon>());
            var card = Assert.Single(notice.GetVisualDescendants().OfType<AppCard>());
            var origin = text.TranslatePoint(new Point(0, 0), notice)!.Value;
            Assert.Equal(notice.Hint, text.Text);
            Assert.True(text.Bounds.Height > 20);
            Assert.InRange(origin.X + text.Bounds.Width, 0, notice.Bounds.Width);
            Assert.Equal(12, icon.Bounds.Width);
            Assert.Equal(12, icon.Bounds.Height);
            Assert.True(card.BoxShadow.Count > 0);
            notice.Hint = WorkspaceFailureText.Get(TaskFailureReason.ProviderTimeout);
            window.UpdateLayout();
            Assert.Equal(notice.Hint, text.Text);
            Assert.DoesNotContain("stream_line_limit", text.Text, StringComparison.Ordinal);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
