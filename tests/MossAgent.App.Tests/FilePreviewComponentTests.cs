using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Analyzers;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class FilePreviewComponentTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 300)]
    [InlineData(true, 300)]
    [InlineData(false, 640)]
    [InlineData(true, 640)]
    public Task PreviewTabsAndLines_AreBoundedAlignedAndThemed(bool dark, int width) => _session.DispatchAsync(async () =>
    {
        using var pane = new FilePreviewPaneViewModel(new InspectorProbeToolExecutor
        { Execute = (_, _, _) => Task.FromResult(new ToolResult(true, "snapshot", Artifacts: [new("test.cs", "snapshot", "text/plain", 1, "hash")])) },
            new PreviewProbeArtifactReader());
        pane.Attach(new ToolExecutionContext(Guid.NewGuid(), System.IO.Path.GetTempPath(), [System.IO.Path.GetTempPath()], ApprovalPolicy.ReadOnly));
        await pane.OpenAsync("a-long-file-name-for-the-bounded-preview-tab.cs", TestContext.Current.CancellationToken);
        await pane.OpenAsync("test.cs", TestContext.Current.CancellationToken);
        var preview = new AppFilePreview { DataContext = pane };
        var window = new AppWindow
        {
            Content = preview,
            Width = width,
            Height = 450,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            var tabs = preview.GetVisualDescendants().OfType<AppActionButton>().Where(button => button.Classes.Contains("file-tab")).ToArray();
            Assert.Equal(2, tabs.Length);
            Assert.All(tabs, tab => { Assert.Equal(30, tab.Bounds.Height); Assert.True(tab.Bounds.Width <= 180); });
            var code = Assert.Single(preview.GetVisualDescendants().OfType<AppCodePreview>());
            var newTab = preview.FindControl<AppIconButton>("NewTabButton")!;
            newTab.Flyout!.ShowAt(newTab);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var back = preview.FindControl<AppActionButton>("BackButton")!;
            var close = preview.FindControl<AppIconButton>("CloseButton")!;
            Assert.NotNull(back.IconData);
            Assert.Same(pane.HideCommand, back.Command);
            newTab.Flyout.Hide();
            Assert.NotNull(close.IconData);
            Assert.True(close.TranslatePoint(new Point(close.Bounds.Width, 0), window)!.Value.X <= width);
            Assert.All(tabs.SelectMany(tab => tab.GetVisualDescendants().OfType<AppIcon>()).Where(icon => icon.IsEffectivelyVisible), icon => Assert.NotNull(icon.Data));
            var text = Assert.Single(code.GetVisualDescendants().OfType<SyntaxHighlightedSelectableTextBlock>());
            Assert.Equal(12, text.FontSize);
            Assert.Equal(22, text.LineHeight);
            Assert.Contains("Preview", text.Code);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public async Task CodeGuard_RejectsPreviewCompositionAndGeometryReplacement()
    {
        var diagnostics = await VisualPolicyTestCompilation.Analyze("""
            using MossAgent.App.Controls;
            public class Sample
            {
                public void Change(AppCodePreview code, AppFilePreview files, AppImagePreview image, AppFileBreadcrumb breadcrumb)
                {
                    code.Content = new AppText(); code.Width = 500;
                    files.Content = new AppText(); files.Padding = new Avalonia.Thickness(2);
                    image.Content = new AppText(); image.Width = 400;
                    breadcrumb.Children.Clear(); breadcrumb.Width = 400;
                }
            }
            """, new SharedControlAppearanceAnalyzer());
        Assert.Equal(8, diagnostics.Count(diagnostic => diagnostic.Id == "MOSSUI002"));
    }
}
