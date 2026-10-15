using Avalonia.Headless;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class WorkspaceTaskMutationSendTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public Task PendingRenameOrArchive_BlocksOnlyItsTargetUntilPublication(bool archive, bool selectOther) =>
        _session.DispatchAsync(async () =>
        {
            await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
            var project = fixture.Workspace.Catalog.SelectedProject!;
            var now = DateTimeOffset.UtcNow;
            var target = new AgentTask(Guid.NewGuid(), project.Id, "target", ApprovalPolicy.ReadOnly,
                AgentTaskStatus.Completed, now, now, null);
            var other = target with { Id = Guid.NewGuid(), Title = "other" };
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var repository = new TestWorkspaceRepository
            {
                ProjectItems = [project],
                SaveTaskHandler = (_, _) => completion.Task,
                ArchiveHandler = (_, _, _) => completion.Task
            };
            repository.TasksByProject[project.Id] = [target, other];
            using var workspace = fixture.CreateWorkspace(repository, fixture.Runs, catalogRepository: repository);
            await workspace.LoadAsync();
            workspace.Catalog.UpsertTask(selectOther ? other : target);
            workspace.ComposerText = "retained prompt";
            await fixture.WaitUntilAsync(() => workspace.ModelPicker.CanSendWithSelectedModel);
            Assert.True(workspace.PrimaryActionCommand.CanExecute(null));
            var availabilityChanges = 0;
            workspace.PrimaryActionCommand.CanExecuteChanged += (_, _) => availabilityChanges++;
            var management = workspace.Catalog.TaskManagement;
            management.TaskRenamed += _ => Assert.Equal(selectOther, workspace.PrimaryActionCommand.CanExecute(null));
            management.BeginRenameCommand.Execute(target);
            management.EditingTitle = "new title";
            var pending = archive
                ? management.ArchiveCommand.ExecuteAsync(target)
                : management.SaveRenameCommand.ExecuteAsync(null);
            try
            {
                Assert.Equal(selectOther, workspace.PrimaryActionCommand.CanExecute(null));
                if (!selectOther) workspace.PrimaryActionCommand.Execute(null);
                Assert.Empty(fixture.Server.Requests);
                Assert.False(workspace.IsRunning);
            }
            finally { completion.TrySetResult(); }
            await pending;
            workspace.ComposerText = "next message";
            Assert.True(workspace.PrimaryActionCommand.CanExecute(null));
            Assert.True(availabilityChanges >= 2);
            Assert.Empty(fixture.Server.Requests);
        }, TestContext.Current.CancellationToken);
}
