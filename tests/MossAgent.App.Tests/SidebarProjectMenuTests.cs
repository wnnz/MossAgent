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
using MossAgent.Application.Settings;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class SidebarProjectMenuTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ProjectMenu_ControlsRealSortingWithRightAlignedChecks(bool dark) => _session.DispatchAsync(async () =>
    {
        var root = Directory.CreateTempSubdirectory("MossAgent-sidebar-sort-menu-").FullName;
        try
        {
            await using var services = AppComposition.CreateServices(root);
            var token = TestContext.Current.CancellationToken;
            await services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(token);
            var repository = services.GetRequiredService<IWorkspaceRepository>();
            var first = new ProjectProfile(Guid.NewGuid(), "first", root, [root], DateTimeOffset.UtcNow);
            var second = first with { Id = Guid.NewGuid(), Name = "second", CreatedAt = first.CreatedAt.AddDays(-1) };
            await repository.SaveProjectAsync(first, token);
            await repository.SaveProjectAsync(second, token);
            var model = services.GetRequiredService<MainWindowViewModel>();
            await model.Workspace.LoadAsync();
            var window = new MainWindow
            {
                DataContext = model,
                Width = 980,
                Height = 640,
                RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
            };
            window.Show();
            try
            {
                window.UpdateLayout();
                var sidebar = Assert.Single(window.GetVisualDescendants().OfType<WorkspaceSidebar>());
                var options = Assert.Single(sidebar.GetVisualDescendants().OfType<AppIconButton>(), button => button.Label == "项目选项");
                var flyout = Assert.IsType<AppFlyout>(options.Flyout);
                flyout.ShowAt(options);
                window.UpdateLayout();
                var card = Assert.IsType<AppCard>(flyout.Content);
                card.UpdateLayout();
                var actions = card.GetVisualDescendants().OfType<AppActionButton>().ToArray();
                Assert.Equal(2, actions.Length);
                Assert.Equal(model.Workspace.SidebarProjects.SortByActivityLabel, actions[0].Label);
                Assert.Same(model.Workspace.SidebarProjects.SortByActivityCommand, actions[0].Command);
                Assert.Same(model.Workspace.SidebarProjects.SortManuallyCommand, actions[1].Command);
                Assert.True(actions[0].IsTrailingIconVisible);
                Assert.False(actions[1].IsTrailingIconVisible);
                var check = Assert.Single(actions[0].GetVisualDescendants().OfType<AppIcon>(), icon => icon.Classes.Contains("action-trailing-icon"));
                var checkX = check.TranslatePoint(default, actions[0])!.Value.X;
                Assert.True(checkX > actions[0].Bounds.Width - 30);
                await model.Workspace.SidebarProjects.SortManuallyCommand.ExecuteAsync(null);
                Assert.False(actions[0].IsTrailingIconVisible);
                Assert.True(actions[1].IsTrailingIconVisible);
                flyout.Hide();
                var selected = model.Workspace.Catalog.SelectedProject;
                var moving = model.Workspace.SidebarProjects.Items[1];
                moving.IsExpanded = true;
                await model.Workspace.SidebarProjects.MoveUpCommand.ExecuteAsync(moving);
                Assert.Same(moving, model.Workspace.SidebarProjects.Items[0]);
                Assert.True(moving.IsExpanded);
                Assert.Equal(selected, model.Workspace.Catalog.SelectedProject);
                var saved = await services.GetRequiredService<ISidebarPreferenceStore>().LoadAsync(token);
                Assert.Equal(SidebarProjectSortOrder.Manual, saved.ProjectSortOrder);
                Assert.Equal(moving.Project.Id, saved.ManualProjectOrder[0]);
                window.UpdateLayout();
                var rendered = sidebar.GetVisualDescendants().OfType<AppProjectNavigationRow>().First();
                Assert.Equal(moving.Project.Name, rendered.Title);
            }
            finally { window.Close(); }
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }, TestContext.Current.CancellationToken);
}
