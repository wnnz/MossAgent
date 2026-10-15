using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class SidebarNavigationViewportTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 980)]
    [InlineData(false, 1280)]
    [InlineData(true, 980)]
    [InlineData(true, 1280)]
    public Task ManyPinnedTasks_DoNotCreateANestedViewportOrHideLaterSections(bool dark, double width) =>
        WithWindow(dark, width, (window, model) =>
        {
            var sidebar = Assert.Single(window.GetVisualDescendants().OfType<WorkspaceSidebar>());
            var scroll = Assert.Single(sidebar.GetVisualDescendants().OfType<AppScrollViewer>(),
                item => item.Classes.Contains("sidebar-navigation-scroll"));
            var header = Assert.Single(sidebar.GetVisualDescendants().OfType<AppBorder>(),
                item => item.Classes.Contains("projects-section-header"));
            Assert.Single(sidebar.GetVisualDescendants().OfType<AppScrollViewer>());
            Assert.Equal(16, model.Workspace.Catalog.TaskManagement.PinnedTasks.Items.Count);
            Assert.True(scroll.Extent.Height > scroll.Viewport.Height);
            var thumb = Assert.Single(scroll.GetVisualDescendants().OfType<AppBorder>(),
                item => item.Classes.Contains("vertical-scroll-thumb"));
            Assert.Equal(6, thumb.Width);
            Assert.Equal(new CornerRadius(3), thumb.CornerRadius);
            Assert.NotNull(thumb.Background);
            var thumbRight = thumb.TranslatePoint(new Point(thumb.Bounds.Width, 0), sidebar)!.Value.X;
            Assert.Equal(sidebar.Bounds.Width - 3, thumbRight, precision: 3);
            var before = header.TranslatePoint(default, sidebar)!.Value.Y;
            scroll.Offset = new Vector(0, scroll.Extent.Height - scroll.Viewport.Height);
            window.UpdateLayout();
            Assert.True(scroll.Offset.Y > 160);
            Assert.Equal(before - scroll.Offset.Y, header.TranslatePoint(default, sidebar)!.Value.Y, precision: 3);
            var last = sidebar.GetVisualDescendants().OfType<AppTaskNavigationRow>().Last();
            var position = last.TranslatePoint(default, scroll)!.Value;
            Assert.True(position.Y + last.Bounds.Height <= scroll.Viewport.Height + 1);
            Assert.True(last.Bounds.Width <= scroll.Viewport.Width);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ClosedProjects_UseDenseRowsWhileExpandedProjectsRetainGroupSpacing(bool dark) =>
        WithWindow(dark, 1280, (window, model) =>
        {
            var sidebar = Assert.Single(window.GetVisualDescendants().OfType<WorkspaceSidebar>());
            var groups = sidebar.GetVisualDescendants().OfType<StackPanel>()
                .Where(panel => panel.Classes.Contains("sidebar-project-group")).ToArray();
            Assert.Equal(2, groups.Length);
            Assert.Equal(9, groups[0].Margin.Bottom);
            Assert.Equal(0, groups[1].Margin.Bottom);
            Assert.Equal(0, groups[0].Spacing);
            foreach (var group in model.Workspace.Catalog.ProjectNavigation) group.IsExpanded = false;
            window.UpdateLayout();
            Assert.All(groups, panel => Assert.Equal(0, panel.Margin.Bottom));
            var first = groups[0].TranslatePoint(default, sidebar)!.Value.Y;
            var second = groups[1].TranslatePoint(default, sidebar)!.Value.Y;
            Assert.Equal(31, second - first, precision: 3);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task LongClosedProjectList_DoesNotAccumulateAnExtraPixelPerGroup(bool dark) =>
        WithWindow(dark, 1280, (window, model) =>
        {
            var source = model.Workspace.Catalog.ProjectNavigation;
            for (var index = 0; index < 10; index++)
                source.Add(SidebarProjectOrderingTests.Project("rhythm " + index, index));
            foreach (var group in source) group.IsExpanded = false;
            window.UpdateLayout();
            var sidebar = Assert.Single(window.GetVisualDescendants().OfType<WorkspaceSidebar>());
            var positions = sidebar.GetVisualDescendants().OfType<AppProjectNavigationRow>()
                .Select(row => row.TranslatePoint(default, sidebar)!.Value.Y).Order().ToArray();
            Assert.Equal(12, positions.Length);
            Assert.Equal(341, positions[^1] - positions[0], precision: 3);
            for (var index = 1; index < positions.Length; index++)
                Assert.Equal(31, positions[index] - positions[index - 1], precision: 3);
        });

    private Task WithWindow(bool dark, double width, Action<MainWindow, MainWindowViewModel> action) =>
        _session.DispatchAsync(async () =>
        {
            var root = Directory.CreateTempSubdirectory("MossAgent-sidebar-viewport-").FullName;
            try
            {
                await using var services = AppComposition.CreateServices(root);
                var cancellation = TestContext.Current.CancellationToken;
                await services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(cancellation);
                var repository = services.GetRequiredService<IWorkspaceRepository>();
                var primary = await SeedAsync(repository, root, cancellation);
                var model = services.GetRequiredService<MainWindowViewModel>();
                await model.Workspace.Catalog.ReloadAsync();
                model.Workspace.Catalog.SelectedProject = primary;
                foreach (var group in model.Workspace.Catalog.ProjectNavigation)
                    group.IsExpanded = group.Project.Id == primary.Id;
                var window = new MainWindow
                {
                    DataContext = model,
                    Width = width,
                    Height = 560,
                    WindowState = WindowState.Normal,
                    RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
                };
                window.Show();
                try { window.UpdateLayout(); action(window, model); }
                finally { window.Close(); }
            }
            finally
            {
                SqliteTestPool.Clear(root);
                Directory.Delete(root, recursive: true);
            }
        }, TestContext.Current.CancellationToken);

    private static async Task<ProjectProfile> SeedAsync(IWorkspaceRepository repository, string root, CancellationToken token)
    {
        var now = DateTimeOffset.UtcNow;
        var primary = new ProjectProfile(Guid.NewGuid(), "A sidebar fixture", root, [root], now);
        var secondary = primary with { Id = Guid.NewGuid(), Name = "B sidebar fixture" };
        foreach (var project in new[] { primary, secondary }) await repository.SaveProjectAsync(project, token);
        for (var index = 0; index < 20; index++)
        {
            var task = new AgentTask(Guid.NewGuid(), index < 18 ? primary.Id : secondary.Id,
                $"Sidebar viewport task {index:00}", ApprovalPolicy.ReadOnly, AgentTaskStatus.Completed,
                now, now.AddSeconds(-index), null);
            await repository.SaveTaskAsync(task, token);
            if (index < 16) await repository.SetTaskPinnedAsync(task.Id, true, token);
        }
        return primary;
    }
}
