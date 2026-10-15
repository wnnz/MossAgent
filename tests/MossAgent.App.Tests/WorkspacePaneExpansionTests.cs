using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class WorkspacePaneExpansionTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 660)]
    [InlineData(true, 660)]
    [InlineData(false, 1000)]
    [InlineData(true, 1000)]
    [InlineData(false, 1600)]
    [InlineData(true, 1600)]
    public Task ExpansionRetainsPanesAndResizedWidthWhileFillingTheWorkspace(bool dark, int width) => _session.Dispatch(() =>
    {
        var primary = new AppInput { Text = "unsent draft" };
        var inspector = new AppBorder();
        var panes = new AppWorkspacePanes { PrimaryPane = primary, InspectorPane = inspector, IsInspectorOpen = true };
        var window = new AppWindow { Width = width, Height = 500, Content = panes, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            window.UpdateLayout();
            var layout = (Grid)panes.Content!;
            layout.ColumnDefinitions[2].Width = new GridLength(inspector.Bounds.Width - 10);
            window.UpdateLayout();
            var preferred = inspector.Bounds.Width;
            panes.ToggleInspectorExpandedCommand.Execute(null);
            window.UpdateLayout();
            Assert.True(panes.IsInspectorExpanded);
            Assert.Equal(panes.Bounds.Width, inspector.Bounds.Width, 3);
            Assert.False(primary.IsEffectivelyVisible);
            Assert.False(Assert.Single(panes.GetVisualDescendants().OfType<AppPaneSplitter>()).IsEffectivelyVisible);
            window.Width = 1800;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Assert.Equal(1800, inspector.Bounds.Width, 3);
            panes.ToggleInspectorExpandedCommand.Execute(null);
            window.UpdateLayout();
            Assert.InRange(inspector.Bounds.Width - preferred, -0.5, 0.5);
            Assert.True(primary.IsEffectivelyVisible);
            Assert.Equal("unsent draft", primary.Text);
            Assert.Same(primary, panes.PrimaryPane);
            Assert.Same(inspector, panes.InspectorPane);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("hidden")]
    [InlineData("disabled")]
    [InlineData("detached")]
    [InlineData("closed")]
    [InlineData("replacement")]
    public Task OwnerInvalidationRestoresLayoutAndRejectsStaleCommands(string change) => _session.Dispatch(() =>
    {
        var panes = new AppWorkspacePanes { PrimaryPane = new AppBorder(), InspectorPane = new AppBorder(), IsInspectorOpen = true };
        var host = new AppBorder { Child = panes };
        var window = new AppWindow { Width = 1000, Height = 500, Content = host };
        window.Show();
        try
        {
            window.UpdateLayout();
            panes.ToggleInspectorExpandedCommand.Execute(null);
            Assert.True(panes.IsInspectorExpanded);
            switch (change)
            {
                case "hidden": host.IsVisible = false; break;
                case "disabled": host.IsEnabled = false; break;
                case "detached": host.Child = null; break;
                case "closed": panes.IsInspectorOpen = false; break;
                case "replacement": panes.InspectorPane = new AppBorder(); break;
            }
            window.UpdateLayout();
            Assert.False(panes.IsInspectorExpanded);
            if (change != "replacement")
            {
                Assert.False(panes.ToggleInspectorExpandedCommand.CanExecute(null));
                panes.ToggleInspectorExpandedCommand.Execute(null);
                Assert.False(panes.IsInspectorExpanded);
            }
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
