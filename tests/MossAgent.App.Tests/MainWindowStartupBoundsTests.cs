using Avalonia.Controls;
using Avalonia.Headless;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class MainWindowStartupBoundsTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task Construction_EstablishesNormalRestoreBoundsBeforeStartupMaximizes() => _session.DispatchAsync(() =>
    {
        var window = new MainWindow();
        Assert.Equal(WindowState.Normal, window.WindowState);
        Assert.Equal(1280, window.Width);
        Assert.Equal(820, window.Height);
        window.Close();
        return Task.CompletedTask;
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(980, 640)]
    [InlineData(1280, 820)]
    public Task ExplicitNormalSize_IsNotOverriddenWhenShown(double width, double height) => _session.DispatchAsync(() =>
    {
        var window = new MainWindow { WindowState = WindowState.Normal, Width = width, Height = height };
        window.Show();
        try
        {
            window.UpdateLayout();
            Assert.Equal(WindowState.Normal, window.WindowState);
            Assert.Equal(width, window.ClientSize.Width);
            Assert.Equal(height, window.ClientSize.Height);
        }
        finally { window.Close(); }
        return Task.CompletedTask;
    }, TestContext.Current.CancellationToken);
}
