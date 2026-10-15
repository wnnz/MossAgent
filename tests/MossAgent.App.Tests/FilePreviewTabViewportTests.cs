using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class FilePreviewTabViewportTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 980)]
    [InlineData(true, 980)]
    [InlineData(false, 1280)]
    [InlineData(true, 1280)]
    public Task ActiveTabRemainsVisibleAfterExpandedResizeAndRestore(bool dark, int width) => _session.DispatchAsync(async () =>
    {
        using var pane = new FilePreviewPaneViewModel(new InspectorProbeToolExecutor
        {
            Execute = (_, _, _) => Task.FromResult(new ToolResult(true, "preview", Artifacts: [new("file", "snapshot", "text/plain", 0, "hash")]))
        }, new PreviewProbeArtifactReader());
        pane.Attach(new ToolExecutionContext(Guid.NewGuid(), Path.GetTempPath(), [Path.GetTempPath()], ApprovalPolicy.ReadOnly));
        for (var index = 0; index < 4; index++)
            await pane.OpenAsync($"preview-{index}-long-file-name.cs", TestContext.Current.CancellationToken);
        var preview = new AppFilePreview { DataContext = pane };
        var panes = new AppWorkspacePanes { PrimaryPane = new AppInput(), InspectorPane = preview, IsInspectorOpen = true };
        preview.Bind(AppFilePreview.IsExpandedProperty, new Binding(nameof(AppWorkspacePanes.IsInspectorExpanded)) { Source = panes });
        preview.ExpansionCommand = panes.ToggleInspectorExpandedCommand;
        var window = new AppWindow
        {
            Content = panes,
            Width = 1600,
            Height = 500,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            Arrange(window);
            panes.ToggleInspectorExpandedCommand.Execute(null);
            Arrange(window);
            window.Width = width;
            Arrange(window);
            panes.ToggleInspectorExpandedCommand.Execute(null);
            Arrange(window);
            var scroller = preview.GetVisualDescendants().OfType<AppScrollViewer>().Single(control => control.Classes.Contains("file-tabs-scroll"));
            var active = preview.GetVisualDescendants().OfType<AppBorder>().Single(control => control.Classes.Contains("file-tab-host") && control.Classes.Contains("selected"));
            var origin = active.TranslatePoint(new Point(0, 0), scroller)!.Value;
            Assert.InRange(origin.X, -0.5, scroller.Bounds.Width);
            Assert.True(origin.X + active.Bounds.Width <= scroller.Bounds.Width + 0.5);
            Assert.Equal("preview-3-long-file-name.cs", pane.Active!.Name);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static void Arrange(AppWindow window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }
}
