using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class BrowserInspectorVisibilityTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task RejectedTabSelection_RestoresTheBoundComboBox() => _session.Dispatch(() =>
    {
        using var fixture = new BrowserToolbarFixture(ApprovalPolicy.ReadOnly);
        var browser = new EmbeddedBrowserPane { DataContext = fixture.Pane };
        var window = new Window { Width = 420, Height = 600, Content = browser };
        window.Show();
        try
        {
            var combo = Assert.Single(browser.GetVisualDescendants().OfType<AppComboBox>());
            var active = fixture.Pane.SelectedTab;
            combo.SelectedItem = fixture.Pane.Tabs.Single(tab => !tab.IsActive);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            Assert.Same(active, fixture.Pane.SelectedTab);
            Assert.Same(active, combo.SelectedItem);
            Assert.Equal("capability_denied", Assert.Single(fixture.Audit.Completions).ErrorCode);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(320, false)]
    [InlineData(640, true)]
    public Task BrowserPaneVisibility_UsesItsOwnContextAfterWorkspaceReplacement(double width, bool dark) =>
        _session.Dispatch(() =>
        {
            var workspace = new EmbeddedBrowserWorkspaceViewModel();
            var taskId = Guid.NewGuid();
            workspace.SelectTask(taskId);
            var inspector = new InspectorPane
            {
                DataContext = new
                {
                    EmbeddedBrowsers = workspace,
                    IsRightPanelOpen = true,
                    IsInspectorBrowser = true,
                    IsContextCardVisible = false,
                    Workspace = new { Catalog = new { SelectedTask = new object() }, FilePreview = new { IsOpen = false } }
                }
            };
            var window = new Window
            {
                Width = width,
                Height = 600,
                Content = inspector,
                RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
            };
            window.Show();
            try
            {
                var browser = Assert.Single(inspector.GetVisualDescendants().OfType<EmbeddedBrowserPane>());
                Assert.False(browser.IsVisible);
                using var pane = workspace.Open(taskId);
                using var native = new AppEmbeddedBrowserView(new NavigationEventBackend());
                pane.Attach(native);
                window.UpdateLayout();

                Assert.Same(pane, browser.DataContext);
                Assert.True(browser.IsEffectivelyVisible);
                Assert.True(browser.Bounds.Width > 0);
                Assert.True(browser.Bounds.Height > 0);
                var toolbar = Assert.Single(browser.GetVisualDescendants().OfType<AppBrowserToolbar>());
                Assert.True(toolbar.IsEffectivelyVisible);
                pane.Detach(native);
                window.UpdateLayout();
                Assert.False(browser.IsVisible);
                workspace.SelectTask(null);
                window.UpdateLayout();
                Assert.NotSame(pane, browser.DataContext);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);
}
