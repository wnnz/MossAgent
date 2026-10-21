using Avalonia.Headless;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class RootObjectAuthorizationUiWorkflowTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task ReplacedRootRequiresExplicitKeyboardConfirmationBeforeRealWriteAndNeverReplaysOnReopen(bool dark, bool compact) =>
        _session.DispatchAsync(async () =>
        {
            await using var fixture = await ProjectAuthorizationUiFixture.CreateAsync(dark, compact);
            var project = await ProjectAuthorizationUiInput.CreateProjectAsync(fixture, multipleRoots: false);
            Assert.Single(project.RootAuthorizations);
            await ReplaceRootAfterReadersFinishAsync(fixture);
            ProjectAuthorizationUiInput.Start(fixture, newTask: true);
            await fixture.WaitUntilAsync(() => !fixture.Main.Workspace.IsRunning);
            var refusedTask = fixture.Main.Workspace.Catalog.SelectedTask!;
            Assert.Equal(AgentTaskStatus.Failed, refusedTask.Status);
            Assert.Equal(TaskFailureReason.WorkspaceAuthorizationChanged, refusedTask.FailureReason);
            Assert.Empty(fixture.Server.Requests);
            Assert.False(fixture.Main.Approval.IsPending);
            await ConfirmThroughKeyboardAsync(fixture, project, refusedTask.Id);
            ProjectAuthorizationUiInput.Start(fixture, newTask: false);
            await fixture.WaitUntilAsync(() => fixture.Main.Approval.IsPending);
            Assert.Equal("filesystem.write_file", fixture.Main.Approval.ToolName);
            await fixture.AssertSentinelsAsync(primaryChanged: false);
            SubagentApprovalInteraction.Decide(fixture.Window, fixture.Main.Approval, approve: true, keyboard: true);
            await fixture.WaitUntilAsync(() => !fixture.Main.Workspace.IsRunning);
            await fixture.AssertResultAsync(refusedTask.Id, "project-write-0-0", null, true);
            await fixture.AssertSentinelsAsync(primaryChanged: true);
            Assert.Equal(2, fixture.Server.Requests.Count);
            Assert.Equal(ProjectAuthorizationUiFixture.InitialContent, await File.ReadAllTextAsync(
                Path.Combine(fixture.PrimaryRoot + "-original", "fixture.txt"), fixture.Token));
            var saved = Assert.Single(await fixture.Repository.GetProjectsAsync(fixture.Token), item => item.Id == project.Id);
            var recorded = await fixture.Audits.GetRecentAsync(refusedTask.Id, 20, fixture.Token);
            await fixture.ReopenAsync(refusedTask.Id);
            Assert.Equal(AgentTaskStatus.Completed, fixture.Main.Workspace.Catalog.SelectedTask!.Status);
            Assert.Equal(saved.RootAuthorizations, fixture.Main.Workspace.Catalog.SelectedProject!.RootAuthorizations);
            await fixture.AssertResultAsync(refusedTask.Id, "project-write-0-0", null, true);
            Assert.Equal(recorded.Count(item => item.ToolName == "filesystem.write_file"),
                (await fixture.Audits.GetRecentAsync(refusedTask.Id, 20, fixture.Token)).Count(item => item.ToolName == "filesystem.write_file"));
            Assert.Equal(2, fixture.Server.Requests.Count);
            ProjectAuthorizationUiInput.SaveEvidence(fixture, "root-object-restored-no-replay");
        }, TestContext.Current.CancellationToken);

    private static async Task ReplaceRootAfterReadersFinishAsync(ProjectAuthorizationUiFixture fixture)
    {
        await RootObjectTestDirectory.MoveWhenReadersFinishAsync(fixture.PrimaryRoot, fixture.PrimaryRoot + "-original", fixture.Token);
        Directory.CreateDirectory(fixture.PrimaryRoot);
        await File.WriteAllTextAsync(fixture.PrimaryFile, ProjectAuthorizationUiFixture.InitialContent, fixture.Token);
    }

    private static async Task ConfirmThroughKeyboardAsync(ProjectAuthorizationUiFixture fixture, ProjectProfile project, Guid taskId)
    {
        var composer = Assert.Single(fixture.Window.GetVisualDescendants().OfType<AppInput>(), input => input.Classes.Contains("composer-input"));
        ProviderUiWorkflowInput.Type(fixture.Window, composer, "前台草稿必须保留");
        ProjectAuthorizationUiInput.OpenEditor(fixture, project.Id);
        var editor = ProjectAuthorizationUiInput.EditorContent(fixture);
        ProviderUiWorkflowInput.Type(fixture.Window, Assert.Single(editor.GetVisualDescendants().OfType<AppInput>()), "项目名称草稿");
        await ProjectAuthorizationUiInput.AddDirectoryAsync(fixture, fixture.AdditionalRoot);
        var confirm = Assert.Single(editor.GetVisualDescendants().OfType<AppButton>(),
            button => ReferenceEquals(button.Command, fixture.Editor.ConfirmRootObjectsCommand));
        Assert.True(Assert.Single(editor.GetVisualDescendants().OfType<AppText>(), text => text.Text == fixture.PrimaryRoot).IsEffectivelyVisible);
        Assert.False(confirm.IsEffectivelyEnabled);
        ProjectAuthorizationUiInput.RemoveDirectory(fixture, fixture.AdditionalRoot);
        ProjectAuthorizationUiInput.SaveEvidence(fixture, "root-object-before-confirmation");
        ProjectAuthorizationUiInput.Press(fixture, confirm);
        await fixture.Editor.ConfirmRootObjectsCommand.ExecutionTask!.WaitAsync(fixture.Token);
        ProviderUiWorkflowInput.Flush(fixture.Window);
        Assert.False(fixture.Editor.HasStatusError, fixture.Editor.Status);
        Assert.True(fixture.Editor.IsOpen);
        Assert.Equal("项目名称草稿", fixture.Editor.Name);
        Assert.Equal("前台草稿必须保留", fixture.Main.Workspace.ComposerText);
        Assert.Equal(taskId, fixture.Main.Workspace.Catalog.SelectedTask?.Id);
        var saved = Assert.Single(await fixture.Repository.GetProjectsAsync(fixture.Token), item => item.Id == project.Id);
        Assert.Equal(2, saved.AuthorizationRevision);
        Assert.NotEqual(project.RootAuthorizations[0].Identity.FileId, saved.RootAuthorizations[0].Identity.FileId);
        Assert.Equal(project.Name, saved.Name);
        Assert.Empty(fixture.Server.Requests);
        ProjectAuthorizationUiInput.SaveEvidence(fixture, "root-object-confirmed-drafts-preserved");
        ProjectAuthorizationUiInput.Press(fixture, Assert.Single(editor.GetVisualDescendants().OfType<AppButton>(),
            button => ReferenceEquals(button.Command, fixture.Editor.CloseCommand)));
        Assert.False(fixture.Editor.IsOpen);
    }
}
