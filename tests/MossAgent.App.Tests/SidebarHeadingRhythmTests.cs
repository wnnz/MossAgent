using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
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
public sealed class SidebarHeadingRhythmTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 980, false)]
    [InlineData(false, 980, true)]
    [InlineData(false, 1280, false)]
    [InlineData(false, 1280, true)]
    [InlineData(true, 980, false)]
    [InlineData(true, 980, true)]
    [InlineData(true, 1280, false)]
    [InlineData(true, 1280, true)]
    public Task PinnedHeading_UsesReferenceInsetWithoutMovingItsTaskOrProjects(bool dark, double width, bool pinned) =>
        WithWindow(dark, width, pinned, (window, model, task) =>
        {
            AssertRhythm(window, pinned);
            Assert.Equal(pinned, model.Workspace.Catalog.TaskManagement.PinnedTasks.HasItems);
            return Task.CompletedTask;
        });

    [Theory]
    [InlineData(false, 980)]
    [InlineData(false, 1280)]
    [InlineData(true, 980)]
    [InlineData(true, 1280)]
    public Task PinAndUnpin_RefreshTheInsetWithoutChangingSelectedTask(bool dark, double width) =>
        WithWindow(dark, width, false, async (window, model, task) =>
        {
            var catalog = model.Workspace.Catalog;
            var project = catalog.SelectedProject;
            var selection = Assert.IsType<AgentTask>(catalog.SelectedTask);
            AssertRhythm(window, false);
            await catalog.TaskManagement.TogglePinCommand.ExecuteAsync(task);
            Refresh(window);
            AssertRhythm(window, true);
            Assert.Equal(selection.Id, catalog.SelectedTask?.Id);
            var pinned = Assert.Single(catalog.TaskManagement.PinnedTasks.Items).Task;
            await catalog.TaskManagement.TogglePinCommand.ExecuteAsync(pinned);
            Refresh(window);
            AssertRhythm(window, false);
            Assert.Empty(catalog.TaskManagement.PinnedTasks.Items);
            Assert.Same(project, catalog.SelectedProject);
            Assert.Equal(selection, catalog.SelectedTask);
        });

    private static void AssertRhythm(MainWindow window, bool pinned)
    {
        var sidebar = Assert.Single(window.GetVisualDescendants().OfType<WorkspaceSidebar>());
        var scroll = Assert.Single(sidebar.GetVisualDescendants().OfType<AppScrollViewer>(),
            item => item.Classes.Contains("sidebar-navigation-scroll"));
        var heading = Assert.Single(sidebar.GetVisualDescendants().OfType<AppText>(),
            item => item.Classes.Contains("sidebar-pinned-heading"));
        var section = Assert.Single(sidebar.GetVisualDescendants().OfType<StackPanel>(),
            item => item.Classes.Contains("sidebar-pinned-section"));
        var projects = Assert.Single(sidebar.GetVisualDescendants().OfType<AppBorder>(),
            item => item.Classes.Contains("projects-section-header"));
        var project = Assert.Single(sidebar.GetVisualDescendants().OfType<AppProjectNavigationRow>());
        Assert.Equal(pinned ? 145 : 149, scroll.TranslatePoint(default, window)!.Value.Y, precision: 3);
        Assert.Equal(pinned, section.IsVisible);
        Assert.Equal(pinned ? 220 : 149, projects.TranslatePoint(default, window)!.Value.Y, precision: 3);
        Assert.Equal(pinned ? 253 : 182, project.TranslatePoint(default, window)!.Value.Y, precision: 3);
        if (!pinned) return;
        Assert.Equal(145, heading.TranslatePoint(default, window)!.Value.Y, precision: 3);
        Assert.Equal(0, heading.TranslatePoint(default, scroll)!.Value.Y, precision: 3);
        Assert.Null(heading.RenderTransform);
        Assert.Equal(14, heading.FontSize);
        Assert.Equal(18, heading.LineHeight);
        var list = Assert.Single(section.GetVisualDescendants().OfType<AppTaskNavigationRow>());
        Assert.Equal(173, list.TranslatePoint(default, window)!.Value.Y, precision: 3);
        Assert.Equal(30, list.Bounds.Height);
    }

    private Task WithWindow(bool dark, double width, bool pinned,
        Func<MainWindow, MainWindowViewModel, AgentTask, Task> action) => _session.DispatchAsync(async () =>
    {
        var root = Directory.CreateTempSubdirectory("MossAgent-sidebar-heading-").FullName;
        try
        {
            await using var services = AppComposition.CreateServices(root);
            var cancellation = TestContext.Current.CancellationToken;
            await services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(cancellation);
            var repository = services.GetRequiredService<IWorkspaceRepository>();
            var task = await SeedAsync(repository, root, pinned, cancellation);
            var model = services.GetRequiredService<MainWindowViewModel>();
            await model.Workspace.Catalog.ReloadAsync();
            foreach (var group in model.Workspace.Catalog.ProjectNavigation) group.IsExpanded = false;
            var window = new MainWindow
            {
                DataContext = model,
                Width = width,
                Height = 820,
                WindowState = WindowState.Normal,
                RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
            };
            window.Show();
            try { Refresh(window); await action(window, model, task); }
            finally { window.Close(); }
        }
        finally
        {
            SqliteTestPool.Clear(root);
            await DeleteRootAsync(root);
        }
    }, TestContext.Current.CancellationToken);

    private static async Task DeleteRootAsync(string root)
    {
        var started = TimeProvider.System.GetTimestamp();
        while (true)
        {
            try { Directory.Delete(root, recursive: true); return; }
            catch (IOException exception) when ((exception.HResult & 0xffff) is 32 or 33
                && TimeProvider.System.GetElapsedTime(started) < TimeSpan.FromSeconds(3))
            {
                await Task.Delay(50, TestContext.Current.CancellationToken);
            }
        }
    }

    private static async Task<AgentTask> SeedAsync(IWorkspaceRepository repository, string root,
        bool pinned, CancellationToken cancellation)
    {
        var now = DateTimeOffset.UtcNow;
        var project = new ProjectProfile(Guid.NewGuid(), "MossAgent", root, [root], now);
        var task = new AgentTask(Guid.NewGuid(), project.Id, "heading fixture", ApprovalPolicy.ReadOnly,
            AgentTaskStatus.Completed, now, now, null);
        await repository.SaveProjectAsync(project, cancellation);
        await repository.SaveTaskAsync(task, cancellation);
        if (pinned) Assert.True(await repository.SetTaskPinnedAsync(task.Id, true, cancellation));
        return task;
    }

    private static void Refresh(MainWindow window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }
}
