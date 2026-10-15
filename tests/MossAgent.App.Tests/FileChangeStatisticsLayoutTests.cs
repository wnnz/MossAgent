using Avalonia;
using Avalonia.Automation;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Services;
using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class FileChangeStatisticsLayoutTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 280)]
    [InlineData(false, 740)]
    [InlineData(true, 280)]
    [InlineData(true, 740)]
    public async Task Summary_UsesRealStatisticsAndAlignedSharedUndoIcon(bool dark, int width)
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        await _session.DispatchAsync(async () =>
        {
            await fixture.Workspace.LoadAsync();
            fixture.Workspace.FileChanges.Dispose();
            var executor = new FileStatisticsTestExecutor();
            using var summary = new WorkspaceFileChangesViewModel(fixture.Workspace.Catalog, fixture.Panels,
                statisticsService: new WorkspaceFileChangeStatisticsService(executor));
            for (var index = 0; index < 4; index++)
                fixture.Workspace.Messages.Add(ChatMessageViewModel.CreateTool(new("filesystem.write_file", true, "written", null,
                    FileChanges: [new(Path.Combine(fixture.Project.PrimaryDirectory, $"long-directory-name-{index}/source-file.cs"), null, new string('B', 64))])));
            var view = new AppFileChangeSummary { Summary = summary };
            var window = new AppWindow { Width = width, Height = 420, Content = view, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
            window.Show();
            try
            {
                await FileChangeStatisticsStateTests.WaitAsync(() => summary.Statistics.HasTotals);
                window.UpdateLayout();
                Assert.Equal("+12", summary.Statistics.AddedLabel);
                Assert.Equal("-8", summary.Statistics.RemovedLabel);
                var descendants = view.GetVisualDescendants().ToArray();
                var widgets = descendants.OfType<AppDiffStatistics>().Where(item => item.IsEffectivelyVisible).ToArray();
                Assert.Equal(4, widgets.Length);
                foreach (var widget in widgets)
                {
                    var texts = widget.GetVisualDescendants().OfType<AppText>().ToArray();
                    Assert.Equal(2, texts.Length);
                    Assert.Equal(texts[0].Bounds.Y, texts[1].Bounds.Y);
                    Assert.Equal(12, texts[0].FontSize);
                    Assert.NotEqual(texts[0].Foreground, texts[1].Foreground);
                    var origin = widget.TranslatePoint(default, view)!.Value;
                    Assert.InRange(origin.X + widget.Bounds.Width, 0, view.Bounds.Width);
                }
                var undo = Assert.Single(descendants.OfType<AppActionButton>(), item => item.Classes.Contains("file-change-action"));
                var icon = Assert.Single(undo.GetVisualDescendants().OfType<AppIcon>(), item => item.Classes.Contains("action-trailing-icon"));
                Assert.Equal(12, icon.Size);
                Assert.NotNull(icon.Data);
                Assert.Same(summary.Undo.RequestCommand, undo.Command);
                Assert.False(undo.IsEffectivelyEnabled);
                var review = Assert.Single(descendants.OfType<AppButton>(), item => ReferenceEquals(item.Command, summary.ReviewCommand));
                var undoOrigin = undo.TranslatePoint(default, view)!.Value;
                var reviewOrigin = review.TranslatePoint(default, view)!.Value;
                Assert.Equal(undoOrigin.Y + undo.Bounds.Height / 2, reviewOrigin.Y + review.Bounds.Height / 2);
                Assert.True(undoOrigin.X + undo.Bounds.Width <= reviewOrigin.X);
                Assert.InRange(reviewOrigin.X + review.Bounds.Width, 0, view.Bounds.Width);
                AssertFileNames(view, summary, 3);
                summary.ExpandCommand.Execute(null);
                window.UpdateLayout();
                Assert.Equal(5, view.GetVisualDescendants().OfType<AppDiffStatistics>().Count(item => item.IsEffectivelyVisible));
                AssertFileNames(view, summary, 4);
                await summary.StopAsync();
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);
    }

    private static void AssertFileNames(AppFileChangeSummary view, WorkspaceFileChangesViewModel summary, int count)
    {
        var rows = view.GetVisualDescendants().OfType<AppButton>().Where(item => item.Classes.Contains("file-change-file")).ToArray();
        Assert.Equal(count, rows.Length);
        foreach (var row in rows)
        {
            var file = Assert.IsType<WorkspaceFileChangeViewModel>(row.DataContext);
            Assert.Equal(file.DisplayPath, AutomationProperties.GetName(row));
            Assert.Same(summary.OpenFileCommand, row.Command);
            Assert.Equal(file.Path, row.CommandParameter);
        }
    }
}
