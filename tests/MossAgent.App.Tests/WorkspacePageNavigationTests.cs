using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class WorkspacePageNavigationTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData("task", 980, false)]
    [InlineData("task", 1280, true)]
    [InlineData("recent", 980, false)]
    [InlineData("recent", 1280, true)]
    [InlineData("new", 980, true)]
    [InlineData("new", 1280, false)]
    public Task ExplicitConversationAction_ReturnsFromAutomationsEvenWhenSelectionDoesNotChange(string action, int width, bool dark) =>
        _session.DispatchAsync(async () =>
        {
            await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
            var newConversationDraft = fixture.Workspace.ComposerText;
            var task = await SelectTaskAsync(fixture);
            await fixture.Workspace.Drafts.LoadCurrentAsync();
            fixture.Workspace.ComposerText = "现有对话中尚未发送的独立草稿";
            await OpenAutomationsAsync(fixture);
            var draft = fixture.Workspace.ComposerText;
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
                Assert.False(window.FindControl<AppNavigationPanes>("WorkspaceNavigation")!.IsEffectivelyVisible);
                if (action == "new")
                    Activate(Assert.Single(window.GetVisualDescendants().OfType<AppIconButton>(), button =>
                        button.IsEffectivelyVisible && button.Classes.Contains("navigation-rail-button") && button.Label == "新对话"));
                else
                {
                    var catalog = fixture.Workspace.Catalog;
                    var rows = action == "task" ? catalog.ProjectNavigation.SelectMany(project => project.Tasks) : catalog.RecentTasks.Items;
                    var target = Assert.Single(rows, row => row.Task.Id == task.Id).Task;
                    Assert.True(catalog.SelectTaskCommand.CanExecute(target));
                    catalog.SelectTaskCommand.Execute(target);
                }
                window.UpdateLayout();
                Assert.False(fixture.Main.Pages.IsAutomationsOpen);
                Assert.False(fixture.Main.Pages.Automations!.History.IsVisible);
                Assert.Equal(action == "new" ? (Guid?)null : task.Id, fixture.Workspace.Catalog.SelectedTask?.Id);
                Assert.Equal(action == "new" ? newConversationDraft : draft, fixture.Workspace.ComposerText);
                if (action == "new")
                {
                    fixture.Workspace.Catalog.SelectTaskCommand.Execute(task);
                    Assert.Equal(draft, fixture.Workspace.ComposerText);
                }
                Assert.Empty(fixture.Server.Requests);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("empty")]
    [InlineData("project")]
    [InlineData("back")]
    public Task ExplicitNavigation_ReturnsToTheWorkspaceWhenTheTargetHasNoTask(string target) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var catalog = fixture.Workspace.Catalog;
        if (target == "back")
        {
            var task = await SelectTaskAsync(fixture);
            catalog.NewTaskCommand.Execute(null);
            catalog.SelectTaskCommand.Execute(task);
        }
        var project = catalog.SelectedProject! with { Id = Guid.NewGuid(), Name = "第二个合成项目" };
        if (target == "project") catalog.Projects.Add(project);
        await OpenAutomationsAsync(fixture);
        if (target == "project") catalog.SelectProjectCommand.Execute(project);
        else if (target == "back") catalog.NavigateBackCommand.Execute(null);
        else catalog.NewTaskCommand.Execute(null);
        Assert.False(fixture.Main.Pages.IsAutomationsOpen);
        Assert.Null(catalog.SelectedTask);
        Assert.Empty(fixture.Server.Requests);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("rename")]
    [InlineData("reload")]
    public Task BackgroundCatalogChanges_DoNotStealTheAutomationsPage(string change) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var task = await SelectTaskAsync(fixture);
        await OpenAutomationsAsync(fixture);
        if (change == "rename") fixture.Workspace.Catalog.UpsertTask(task with { Title = "后台标题更新" });
        else await fixture.Workspace.Catalog.ReloadAsync();
        await fixture.WaitUntilAsync(() => fixture.Workspace.Catalog.SelectedTask is not null);
        Assert.True(fixture.Main.Pages.IsAutomationsOpen);
        Assert.True(fixture.Main.Pages.Automations!.History.IsVisible);
        Assert.Empty(fixture.Server.Requests);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("locked-task")]
    [InlineData("locked-new")]
    [InlineData("locked-project")]
    [InlineData("foreign-task")]
    [InlineData("null-task")]
    [InlineData("disposed")]
    public Task RejectedOrDisposedNavigation_DoesNotChangeThePage(string state) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var task = await SelectTaskAsync(fixture);
        var catalog = fixture.Workspace.Catalog;
        var project = catalog.SelectedProject! with { Id = Guid.NewGuid() };
        catalog.Projects.Add(project);
        await OpenAutomationsAsync(fixture);
        if (state.StartsWith("locked", StringComparison.Ordinal)) catalog.IsLocked = true;
        if (state == "disposed") fixture.Main.Pages.Dispose();
        if (state == "locked-new") catalog.NewTaskCommand.Execute(null);
        else if (state == "locked-project") catalog.SelectProjectCommand.Execute(project);
        else catalog.SelectTaskCommand.Execute(state == "foreign-task" ? task with { ProjectId = Guid.NewGuid() }
            : state == "null-task" ? null : task);
        Assert.True(fixture.Main.Pages.IsAutomationsOpen);
        Assert.Equal(task.Id, catalog.SelectedTask!.Id);
        Assert.Empty(fixture.Server.Requests);
        catalog.IsLocked = false;
    }, TestContext.Current.CancellationToken);

    private static async Task<AgentTask> SelectTaskAsync(WorkspaceModelSelectionFixture fixture)
    {
        var now = DateTimeOffset.UtcNow;
        var task = new AgentTask(Guid.NewGuid(), fixture.Workspace.Catalog.SelectedProject!.Id, "合成导航对话",
            ApprovalPolicy.ReadOnly, AgentTaskStatus.Completed, now, now, null);
        await fixture.Tasks.SaveTaskAsync(task, fixture.Token);
        fixture.Workspace.Catalog.UpsertTask(task);
        return task;
    }

    private static async Task OpenAutomationsAsync(WorkspaceModelSelectionFixture fixture)
    {
        fixture.Main.Pages.OpenAutomationsCommand.Execute(null);
        await fixture.Main.Pages.Automations!.ReloadAsync();
        Assert.True(fixture.Main.Pages.IsAutomationsOpen);
    }

    private static void Activate(AppButton button)
    {
        Assert.True(button.Focus(NavigationMethod.Tab));
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
    }
}
