using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class MainWindowResizeLayoutTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, -2)]
    [InlineData(false, -1)]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(false, 3)]
    [InlineData(false, 4)]
    [InlineData(false, 5)]
    [InlineData(false, 6)]
    [InlineData(true, -2)]
    [InlineData(true, -1)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    [InlineData(true, 3)]
    [InlineData(true, 4)]
    [InlineData(true, 5)]
    [InlineData(true, 6)]
    public Task ShrinkAndGrow_KeepActivePagesAndWindowActionsInsideClient(bool dark, int page) =>
        _session.DispatchAsync(async () =>
        {
            await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
            var model = fixture.Main;
            model.IsRightPanelOpen = page == -2;
            if (page >= 0)
            {
                model.IsSettingsOpen = true;
                model.SettingsTabIndex = page;
            }
            var window = new MainWindow
            {
                DataContext = model,
                WindowState = WindowState.Normal,
                Width = 1920,
                Height = 1032,
                RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
            };
            window.Show();
            try
            {
                foreach (var size in new[] { new Size(1920, 1032), new Size(1280, 820), new Size(980, 640), new Size(1920, 1032) })
                {
                    window.Width = size.Width;
                    window.Height = size.Height;
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                    Assert.Equal(size, window.ClientSize);
                    var root = Assert.IsAssignableFrom<Control>(window.Content);
                    Assert.Equal(size, root.Bounds.Size);
                    var actions = window.GetVisualDescendants().OfType<AppIconButton>()
                        .Where(button => button.Classes.Contains("window-control")).ToArray();
                    Assert.Equal(3, actions.Length);
                    Assert.All(actions, button => AssertInsideClient(window, button));
                    AssertActivePage(window, page);
                }
                Assert.Equal("合成请求，验证明确选择的模型。", fixture.Workspace.ComposerText);
                Assert.Empty(fixture.Server.Requests);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);

    private static void AssertActivePage(MainWindow window, int page)
    {
        if (page >= 0)
        {
            AssertInsideClient(window, Assert.Single(window.GetVisualDescendants().OfType<SettingsOverlay>()));
            return;
        }
        AssertInsideClient(window, Assert.Single(window.GetVisualDescendants().OfType<AppNavigationRail>()));
        AssertInsideClient(window, Assert.Single(window.GetVisualDescendants().OfType<WorkspaceChatView>()));
        AssertInsideClient(window, Assert.Single(window.GetVisualDescendants().OfType<WorkspaceComposerView>()));
        if (page == -2)
            AssertInsideClient(window, Assert.Single(window.GetVisualDescendants().OfType<InspectorPane>()));
    }

    private static void AssertInsideClient(MainWindow window, Control control)
    {
        Assert.True(control.IsEffectivelyVisible);
        Assert.True(control.Bounds.Width > 0 && control.Bounds.Height > 0);
        var origin = control.TranslatePoint(default, window)!.Value;
        var end = control.TranslatePoint(new Point(control.Bounds.Width, control.Bounds.Height), window)!.Value;
        Assert.InRange(origin.X, 0, window.ClientSize.Width);
        Assert.InRange(origin.Y, 0, window.ClientSize.Height);
        Assert.InRange(end.X, 0, window.ClientSize.Width);
        Assert.InRange(end.Y, 0, window.ClientSize.Height);
    }
}
