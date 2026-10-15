using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using MossAgent.App.Controls;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class FilePreviewImageMenuOwnershipTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, "context")]
    [InlineData(true, "context")]
    [InlineData(false, "hidden")]
    [InlineData(true, "hidden")]
    [InlineData(false, "disabled")]
    [InlineData(true, "disabled")]
    [InlineData(false, "detached")]
    [InlineData(true, "detached")]
    public Task InvalidatingTheOwnerClosesImageOptionsWithoutReadingAgain(bool dark, string change) => _session.DispatchAsync(async () =>
    {
        using var fixture = new FilePreviewActionFixture(ApprovalPolicy.ReadOnly);
        await fixture.OpenAsync("combo-focus-comparison.png");
        var preview = new AppFilePreview { DataContext = fixture.Pane };
        var host = new AppBorder { Child = preview };
        var window = new AppWindow
        {
            Width = 754,
            Height = 450,
            Content = host,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            var button = preview.FindControl<AppIconButton>("ImageViewButton")!;
            var menu = (AppFlyout)button.Flyout!;
            menu.ShowAt(button);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.True(menu.IsOpen);
            switch (change)
            {
                case "context": preview.DataContext = null; break;
                case "hidden": host.IsVisible = false; break;
                case "disabled": host.IsEnabled = false; break;
                case "detached": host.Child = null; break;
                default: throw new ArgumentOutOfRangeException(nameof(change));
            }
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.False(menu.IsOpen);
            Assert.Single(fixture.Calls);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
