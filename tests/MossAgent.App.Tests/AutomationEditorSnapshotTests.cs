using Avalonia.Headless;
using MossAgent.App.Resources;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class AutomationEditorSnapshotTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task DraftSelection_ShowsPendingSummaryAndExistingSnapshotShowsPinnedGeneration() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var project = fixture.Workspace.Catalog.SelectedProject!;
        var editor = new AutomationEditorViewModel();
        editor.Open(null, [project], [new(fixture.SelectedProvider, fixture.SelectedModel)]);
        Assert.Equal(AutomationText.Get("SnapshotIncomplete"), editor.SnapshotSummary);
        editor.SelectedProject = project;
        editor.SelectedModel = editor.Models[0];
        Assert.Contains(fixture.SelectedProvider.Name, editor.SnapshotSummary);
        Assert.Contains(ApprovalPolicyText.Label(ApprovalPolicy.ReadOnly), editor.SnapshotSummary);
        Assert.Contains($"{project.AuthorizedDirectories.Count}", editor.SnapshotSummary);
        Assert.DoesNotContain("代", editor.SnapshotSummary);

        var snapshot = AutomationAuthorizationSnapshot.Capture(project, fixture.SelectedProvider,
            fixture.SelectedModel, "固定提示词", ApprovalPolicy.FullAccess, 3, DateTimeOffset.UtcNow);
        var stored = AutomationHistoryTests.Item(fixture).Schedule with { AuthorizationSnapshot = snapshot };
        editor.Open(stored, editor.Projects, editor.Models);
        Assert.Contains(fixture.SelectedProvider.Name, editor.SnapshotSummary);
        Assert.Contains(ApprovalPolicyText.Label(ApprovalPolicy.FullAccess), editor.SnapshotSummary);
        Assert.Contains("3", editor.SnapshotSummary);
        Assert.Empty(fixture.Server.Requests);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task LegacyPlanWithoutSnapshot_ShowsMissingHint() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var editor = new AutomationEditorViewModel();
        editor.Open(AutomationHistoryTests.Item(fixture).Schedule,
            [fixture.Workspace.Catalog.SelectedProject!], [new(fixture.SelectedProvider, fixture.SelectedModel)]);
        Assert.Equal(AutomationText.Get("SnapshotMissingHint"), editor.SnapshotSummary);
        Assert.Empty(fixture.Server.Requests);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ItemState_PrefersRunningThenReauthorizationThenScheduleState() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var schedule = AutomationHistoryTests.Item(fixture).Schedule;
        Assert.Equal(AutomationText.Get("Running"),
            new AutomationItemViewModel(schedule, "p", "m", running: true, needsReauthorization: true).StateLabel);
        Assert.Equal(AutomationText.Get("NeedsReauthorization"),
            new AutomationItemViewModel(schedule, "p", "m", false, needsReauthorization: true).StateLabel);
        Assert.Equal(AutomationText.Get("Paused"), new AutomationItemViewModel(schedule, "p", "m", false).StateLabel);
        Assert.Empty(fixture.Server.Requests);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task SavingCapturesSnapshotAndEditingBumpsItsGeneration() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var project = fixture.Workspace.Catalog.SelectedProject!;
        using var page = new AutomationViewModel(fixture.Automations, fixture.Scheduler,
            fixture.Configurations, fixture.Tasks, fixture.Workspace.Catalog);
        await page.CreateCommand.ExecuteAsync(null);
        Assert.True(page.Editor.IsOpen);
        page.Editor.Name = "快照计划";
        page.Editor.Prompt = "固定提示词";
        page.Editor.SelectedProject = Assert.Single(page.Editor.Projects);
        page.Editor.SelectedModel = Assert.Single(page.Editor.Models, item => item.Model.Id == fixture.SelectedModel.Id);
        await page.SaveCommand.ExecuteAsync(null);
        Assert.False(page.HasError, page.Error);
        var stored = Assert.Single(await fixture.Automations.GetAllAsync(fixture.Token));
        Assert.NotNull(stored.AuthorizationSnapshot);
        var snapshot = stored.AuthorizationSnapshot!;
        Assert.Equal(1, snapshot.Generation);
        Assert.Equal(project.Id, snapshot.ProjectId);
        Assert.Equal(project.AuthorizationRevision, snapshot.ProjectAuthorizationRevision);
        Assert.Equal(project.AuthorizedDirectories, snapshot.AuthorizedDirectories);
        Assert.Equal(fixture.SelectedProvider.Id, snapshot.ProviderId);
        Assert.Equal(fixture.SelectedProvider.Name, snapshot.ProviderName);
        Assert.Equal(fixture.SelectedModel.Id, snapshot.ModelId);
        Assert.Equal(ApprovalPolicy.ReadOnly, snapshot.ApprovalPolicy);
        Assert.Equal("固定提示词", snapshot.Prompt);

        await page.EditCommand.ExecuteAsync(Assert.Single(page.Items));
        Assert.True(page.Editor.IsOpen);
        page.Editor.Name = "快照计划二";
        await page.SaveCommand.ExecuteAsync(null);
        Assert.False(page.HasError, page.Error);
        var updated = Assert.Single(await fixture.Automations.GetAllAsync(fixture.Token));
        Assert.NotNull(updated.AuthorizationSnapshot);
        var next = updated.AuthorizationSnapshot!;
        Assert.Equal(2, next.Generation);
        Assert.True(next.CreatedAt >= snapshot.CreatedAt);
        Assert.Empty(fixture.Server.Requests);
    }, TestContext.Current.CancellationToken);
}
