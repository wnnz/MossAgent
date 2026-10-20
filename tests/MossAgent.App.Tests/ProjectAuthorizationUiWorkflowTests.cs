using Avalonia.Headless;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ProjectAuthorizationUiWorkflowTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task SavedRoots_ReachRealToolsAndRevocationSurvivesResumeAndProjectSwitch(bool dark, bool compact) =>
        _session.DispatchAsync(async () =>
        {
            await using var fixture = await ProjectAuthorizationUiFixture.CreateAsync(dark, compact);
            var project = await ProjectAuthorizationUiInput.CreateProjectAsync(fixture, multipleRoots: true);
            ProjectAuthorizationUiInput.Start(fixture, newTask: true);
            await fixture.WaitUntilAsync(() => fixture.Main.Approval.IsPending);
            var taskId = fixture.Main.Workspace.Catalog.SelectedTask!.Id;
            Assert.Equal(fixture.AdditionalRoot, fixture.Main.Approval.WorkingDirectory);
            Assert.Equal("filesystem.write_file", fixture.Main.Approval.ToolName);
            await fixture.AssertSentinelsAsync(primaryChanged: false);
            ProjectAuthorizationUiInput.SaveEvidence(fixture, "secondary-root-pending");
            SubagentApprovalInteraction.Decide(fixture.Window, fixture.Main.Approval, approve: true, keyboard: true);
            await fixture.WaitUntilAsync(() => !fixture.Main.Workspace.IsRunning);
            await fixture.AssertResultAsync(taskId, "project-write-0-0", null, true);
            await fixture.AssertSentinelsAsync(primaryChanged: true);

            ProjectAuthorizationUiInput.OpenEditor(fixture, project.Id);
            ProjectAuthorizationUiInput.RemoveDirectory(fixture, fixture.PrimaryRoot);
            await ProjectAuthorizationUiInput.SaveEditorAsync(fixture);
            var saved = Assert.Single(await fixture.Repository.GetProjectsAsync(fixture.Token), item => item.Id == project.Id);
            Assert.Equal(fixture.AdditionalRoot, Assert.Single(saved.AuthorizedDirectories));
            await ProjectAuthorizationUiInput.SelectTaskAsync(fixture, taskId);
            ProjectAuthorizationUiInput.Start(fixture, newTask: false);
            await fixture.WaitUntilAsync(() => !fixture.Main.Workspace.IsRunning);
            Assert.Equal(taskId, fixture.Main.Workspace.Catalog.SelectedTask?.Id);
            Assert.False(fixture.Main.Approval.IsPending);
            await fixture.AssertResultAsync(taskId, "project-write-1-0", "path_denied", false);
            Assert.Equal(2, (await fixture.Audits.GetRecentAsync(taskId, 10, fixture.Token)).Count(
                audit => audit.ToolName == "filesystem.write_file"));
            Assert.Equal(4, fixture.Server.Requests.Count);
            ProjectAuthorizationUiInput.SaveEvidence(fixture, "removed-root-denied");

            var recordedAudits = await fixture.CaptureAuditsAsync(taskId);
            await fixture.ReopenAsync(taskId);
            await fixture.AssertRestoredAuditsAsync(taskId, recordedAudits);
            Assert.Equal(4, fixture.Server.Requests.Count);
            await fixture.AssertResultAsync(taskId, "project-write-1-0", "path_denied", false);
            ProjectAuthorizationUiInput.SelectProject(fixture, fixture.OtherProject.Id);
            ProjectAuthorizationUiInput.Start(fixture, newTask: true);
            await fixture.WaitUntilAsync(() => !fixture.Main.Workspace.IsRunning);
            var otherTask = fixture.Main.Workspace.Catalog.SelectedTask!;
            Assert.NotEqual(taskId, otherTask.Id);
            Assert.Equal(fixture.OtherProject.Id, otherTask.ProjectId);
            await fixture.AssertResultAsync(otherTask.Id, "project-write-2-0", "path_denied", false);
            Assert.Single(await fixture.Audits.GetRecentAsync(otherTask.Id, 10, fixture.Token));
            Assert.Equal(AgentTaskStatus.Completed, otherTask.Status);
            await fixture.AssertSentinelsAsync(primaryChanged: true);
            await fixture.Server.Completion.WaitAsync(fixture.Token);
            Assert.Equal(6, fixture.Server.Requests.Count);
            Assert.False(fixture.Main.Approval.IsPending);
            ProjectAuthorizationUiInput.SaveEvidence(fixture, "other-project-denied");
        }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task AddingDirectoryDuringApproval_DoesNotExpandAnAlreadyRunningTask(bool dark, bool compact) =>
        _session.DispatchAsync(async () =>
        {
            await using var fixture = await ProjectAuthorizationUiFixture.CreateAsync(dark, compact, expansion: true);
            var project = await ProjectAuthorizationUiInput.CreateProjectAsync(fixture, multipleRoots: false);
            ProjectAuthorizationUiInput.Start(fixture, newTask: true);
            await fixture.WaitUntilAsync(() => fixture.Main.Approval.IsPending);
            var taskId = fixture.Main.Workspace.Catalog.SelectedTask!.Id;
            var requestId = fixture.Main.Approval.CurrentRequestId;
            ProjectAuthorizationUiInput.OpenEditor(fixture, project.Id);
            await ProjectAuthorizationUiInput.AddDirectoryAsync(fixture, fixture.AdditionalRoot);
            ProjectAuthorizationUiInput.SaveEvidence(fixture, "active-task-root-draft");
            await ProjectAuthorizationUiInput.SaveEditorAsync(fixture);
            Assert.Equal(requestId, fixture.Main.Approval.CurrentRequestId);
            var saved = Assert.Single(await fixture.Repository.GetProjectsAsync(fixture.Token), item => item.Id == project.Id);
            Assert.Equal(new[] { fixture.PrimaryRoot, fixture.AdditionalRoot }, saved.AuthorizedDirectories);
            await ProjectAuthorizationUiInput.SelectTaskAsync(fixture, taskId);
            SubagentApprovalInteraction.Decide(fixture.Window, fixture.Main.Approval, approve: true, keyboard: true);
            await fixture.WaitUntilAsync(() => !fixture.Main.Workspace.IsRunning);
            await fixture.AssertResultAsync(taskId, "project-write-0-0", null, true);
            await fixture.AssertResultAsync(taskId, "project-write-0-1", "path_denied", false);
            Assert.Equal(2, (await fixture.Audits.GetRecentAsync(taskId, 10, fixture.Token)).Count(
                audit => audit.ToolName == "filesystem.write_file"));
            Assert.Equal(3, fixture.Server.Requests.Count);
            Assert.False(fixture.Main.Approval.IsPending);
            await fixture.AssertSentinelsAsync(primaryChanged: true);
            ProjectAuthorizationUiInput.SaveEvidence(fixture, "active-task-root-not-expanded");
            ProjectAuthorizationUiInput.Start(fixture, newTask: true);
            await fixture.WaitUntilAsync(() => fixture.Main.Approval.IsPending);
            var nextTask = fixture.Main.Workspace.Catalog.SelectedTask!;
            Assert.NotEqual(taskId, nextTask.Id);
            Assert.Equal(project.Id, nextTask.ProjectId);
            Assert.Equal(fixture.PrimaryRoot, fixture.Main.Approval.WorkingDirectory);
            SubagentApprovalInteraction.Decide(fixture.Window, fixture.Main.Approval, approve: true, keyboard: true);
            await fixture.WaitUntilAsync(() => !fixture.Main.Workspace.IsRunning);
            await fixture.Server.Completion.WaitAsync(fixture.Token);
            await fixture.AssertResultAsync(nextTask.Id, "project-write-1-0", null, true);
            await fixture.AssertSentinelsAsync(primaryChanged: true, additionalChanged: true);
            Assert.Equal(5, fixture.Server.Requests.Count);
            var recordedAudits = await fixture.CaptureAuditsAsync(taskId);
            await fixture.ReopenAsync(taskId);
            await fixture.AssertRestoredAuditsAsync(taskId, recordedAudits);
            Assert.Equal(5, fixture.Server.Requests.Count);
            ProjectAuthorizationUiInput.SaveEvidence(fixture, "original-task-restored-without-replay");
        }, TestContext.Current.CancellationToken);
}
