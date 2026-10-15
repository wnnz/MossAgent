using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class MainWindowPaneLayoutTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 1920, 700, 800, 800, 900)]
    [InlineData(true, 1920, 700, 800, 800, 900)]
    [InlineData(false, 980, 280, 310, 360, 400)]
    [InlineData(true, 980, 280, 310, 360, 400)]
    public Task ExpandedInspector_UsesAvailableWorkspaceRatherThanFixedCardWidth(
        bool dark, double width, double inspectorMinimum, double inspectorMaximum,
        double chatMinimum, double chatMaximum) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        fixture.Main.IsRightPanelOpen = true;
        var window = new MainWindow
        {
            DataContext = fixture.Main,
            WindowState = WindowState.Normal,
            Width = width,
            Height = 820,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            var inspector = Assert.Single(window.GetVisualDescendants().OfType<InspectorPane>());
            Assert.Equal(width, window.ClientSize.Width);
            Assert.InRange(inspector.Bounds.Width, inspectorMinimum, inspectorMaximum);
            var chat = Assert.Single(window.GetVisualDescendants().OfType<WorkspaceChatView>());
            Assert.InRange(chat.Bounds.Width, chatMinimum, chatMaximum);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
