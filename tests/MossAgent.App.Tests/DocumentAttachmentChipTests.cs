using Avalonia.Controls;
using Avalonia.Headless;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class DocumentAttachmentChipTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task KindLabelShowsPdfBadgeAndStaysHiddenWhenEmpty() => _session.Dispatch(() =>
    {
        var chip = new AppAttachmentChip { DisplayPath = "report.pdf", SizeLabel = "12 KiB", KindLabel = "PDF" };
        Assert.True(chip.HasKindLabel);
        var badge = chip.FindControl<AppText>("KindBadge")!;
        Assert.Equal("PDF", badge.Text);
        var window = new AppWindow { Content = chip, Width = 300, Height = 120 };
        window.Show();
        try
        {
            window.UpdateLayout();
            Assert.True(badge.IsVisible);
            chip.KindLabel = string.Empty;
            Assert.False(chip.HasKindLabel);
            Assert.False(badge.IsVisible);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
