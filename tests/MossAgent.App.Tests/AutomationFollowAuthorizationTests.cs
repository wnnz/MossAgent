using System.Text.Json;
using Avalonia.Headless;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class AutomationFollowAuthorizationTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    private static string SystemPrompt(JsonElement request) => request.GetProperty("input")
        .EnumerateArray().First(item => item.GetProperty("role").GetString() == "system")
        .GetProperty("content").GetString()!;

    [Fact]
    public Task Editor_TogglesFollowAuthorizationAndRestoresStoredValue() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var project = fixture.Workspace.Catalog.SelectedProject!;
        var editor = new AutomationEditorViewModel();
        editor.Open(null, [project], [new(fixture.SelectedProvider, fixture.SelectedModel)]);
        Assert.False(editor.FollowAuthorization);
        editor.Name = "跟随计划";
        editor.Prompt = "只读检查。";
        editor.SelectedProject = project;
        editor.SelectedModel = editor.Models[0];
        var revision = editor.DraftRevision;
        editor.FollowAuthorization = true;
        Assert.Equal(revision + 1, editor.DraftRevision);
        var built = editor.Build(DateTimeOffset.UtcNow);
        Assert.True(built.FollowAuthorizationChanges);
        editor.FollowAuthorization = false;
        Assert.False(editor.Build(DateTimeOffset.UtcNow).FollowAuthorizationChanges);

        editor.Open(built with { FollowAuthorizationChanges = true }, editor.Projects, editor.Models);
        Assert.True(editor.FollowAuthorization);
        Assert.True(editor.Build(DateTimeOffset.UtcNow).FollowAuthorizationChanges);
        Assert.Empty(fixture.Server.Requests);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task Item_ReportsFollowAuthorizationOnlyForOptedPlans() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var schedule = AutomationHistoryTests.Item(fixture).Schedule;
        Assert.False(new AutomationItemViewModel(schedule, "p", "m", false).FollowsAuthorization);
        Assert.True(new AutomationItemViewModel(schedule with { FollowAuthorizationChanges = true },
            "p", "m", false).FollowsAuthorization);
        Assert.Empty(fixture.Server.Requests);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task CreateContext_UsesPinnedRootsForContextAndGuard() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var project = fixture.Workspace.Catalog.SelectedProject!;
        var task = new AgentTask(Guid.NewGuid(), project.Id, "钉住任务", ApprovalPolicy.ReadOnly,
            AgentTaskStatus.Running, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null);
        var pinned = new[] { project.PrimaryDirectory };
        var context = fixture.Panels.CreateContext(task, project, pinned);
        Assert.Equal(pinned, context.AuthorizedRoots);
        await context.ValidateWorkspaceAsync(fixture.Token);
        Assert.Empty(fixture.Server.Requests);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task CreateContext_PassesPinnedRootsThroughWithoutProjectRevalidation() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var project = fixture.Workspace.Catalog.SelectedProject!;
        var task = new AgentTask(Guid.NewGuid(), project.Id, "钉住任务", ApprovalPolicy.ReadOnly,
            AgentTaskStatus.Running, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null);
        // Deliberately outside the project's authorization: narrowing to live roots is the
        // authorization gate's single responsibility; the coordinator must pass them verbatim.
        var outside = new[] { Path.Combine(project.PrimaryDirectory, $"outside-{Guid.NewGuid():N}") };
        var context = fixture.Panels.CreateContext(task, project, outside);
        Assert.Equal(outside, context.AuthorizedRoots);
        await context.ValidateWorkspaceAsync(fixture.Token);
        Assert.Empty(fixture.Server.Requests);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task FollowPlan_RunsWithLiveRootsAfterAuthorizationExpansion() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var project = fixture.Workspace.Catalog.SelectedProject!;
        var marker = Path.Combine(project.PrimaryDirectory, "follow-scope-marker");
        Directory.CreateDirectory(marker);
        var schedule = await fixture.AuthorizedAsync(
            AutomationListOwnershipTests.Schedule(fixture) with { FollowAuthorizationChanges = true });
        await fixture.Automations.SaveAsync(schedule, fixture.Token);
        await fixture.Tasks.SaveProjectAsync(project with
        { AuthorizedDirectories = [project.PrimaryDirectory, marker] }, fixture.Token);
        await fixture.Scheduler.StartAsync(fixture.Token);
        Assert.True(await fixture.Scheduler.RunNowAsync(schedule.Id, fixture.Token));
        await fixture.WaitUntilAsync(() => !fixture.Scheduler.IsRunning(schedule.Id));
        var run = Assert.Single(await fixture.Automations.GetRunsAsync(schedule.Id, 50, fixture.Token));
        Assert.Equal(AutomationRunStatus.Completed, run.Status);
        var request = Assert.Single(fixture.Server.Requests);
        Assert.Contains("follow-scope-marker", SystemPrompt(request));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task PinnedPlan_ExecutesWithSnapshotSubsetAfterAuthorizationExpansion() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var project = fixture.Workspace.Catalog.SelectedProject!;
        var marker = Path.Combine(project.PrimaryDirectory, "pinned-scope-marker");
        Directory.CreateDirectory(marker);
        await fixture.Tasks.SaveProjectAsync(project with
        { AuthorizedDirectories = [project.PrimaryDirectory, marker] }, fixture.Token);
        var live = Assert.Single(await fixture.Tasks.GetProjectsAsync(fixture.Token));
        var snapshot = new AutomationAuthorizationSnapshot(1, live.Id, live.AuthorizationRevision,
            [project.PrimaryDirectory], fixture.SelectedProvider.Id, fixture.SelectedProvider.Name,
            fixture.SelectedModel.Id, fixture.SelectedModel.DisplayName, ApprovalPolicy.ReadOnly,
            "只读检查项目。", DateTimeOffset.UtcNow);
        var schedule = AutomationListOwnershipTests.Schedule(fixture) with { AuthorizationSnapshot = snapshot };
        await fixture.Automations.SaveAsync(schedule, fixture.Token);
        await fixture.Scheduler.StartAsync(fixture.Token);
        Assert.True(await fixture.Scheduler.RunNowAsync(schedule.Id, fixture.Token));
        await fixture.WaitUntilAsync(() => !fixture.Scheduler.IsRunning(schedule.Id));
        var run = Assert.Single(await fixture.Automations.GetRunsAsync(schedule.Id, 50, fixture.Token));
        Assert.Equal(AutomationRunStatus.Completed, run.Status);
        var request = Assert.Single(fixture.Server.Requests);
        var system = SystemPrompt(request);
        Assert.DoesNotContain("pinned-scope-marker", system);
        Assert.Contains($"- {project.PrimaryDirectory}", system);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task PinnedPlan_BlocksAfterAuthorizationChange() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var project = fixture.Workspace.Catalog.SelectedProject!;
        var schedule = await fixture.AuthorizedAsync(AutomationListOwnershipTests.Schedule(fixture));
        await fixture.Automations.SaveAsync(schedule, fixture.Token);
        await fixture.Tasks.SaveProjectAsync(project with
        { AuthorizedDirectories = [project.PrimaryDirectory, fixture.DataRoot] }, fixture.Token);
        await fixture.Scheduler.StartAsync(fixture.Token);
        Assert.True(await fixture.Scheduler.RunNowAsync(schedule.Id, fixture.Token));
        await fixture.WaitUntilAsync(() => !fixture.Scheduler.IsRunning(schedule.Id));
        var run = Assert.Single(await fixture.Automations.GetRunsAsync(schedule.Id, 50, fixture.Token));
        Assert.Equal(AutomationRunStatus.Blocked, run.Status);
        Assert.Equal("authorization-revision-changed", run.ErrorCode);
        Assert.Empty(fixture.Server.Requests);
    }, TestContext.Current.CancellationToken);
}
