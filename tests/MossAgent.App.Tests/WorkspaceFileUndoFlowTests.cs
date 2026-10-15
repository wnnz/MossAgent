using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Agent;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceFileUndoFlowTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConfirmedModelEdit_UndoesAndPersistsWithoutReplayingModelOrOverwritingExternalChanges(bool external)
    {
        await using var fixture = await EditedAsync();
        var workspace = fixture.Workspace;
        var requestCount = fixture.Server.Requests.Count;
        if (external) await File.WriteAllTextAsync(fixture.FilePath, "external sentinel", fixture.Token);
        var undo = workspace.FileChanges.Undo;
        Assert.True(undo.RequestCommand.CanExecute(null));
        undo.RequestCommand.Execute(null);
        Assert.True(undo.IsConfirming);
        await undo.ConfirmCommand.ExecuteAsync(null).WaitAsync(fixture.Token);

        Assert.Equal(external ? "external sentinel" : "alpha\n", await File.ReadAllTextAsync(fixture.FilePath, fixture.Token));
        Assert.Equal(external ? 1 : 0, workspace.FileChanges.Files.Count);
        Assert.False(undo.IsBusy);
        Assert.False(undo.HasUnsavedRecord);
        await using var reopened = AppComposition.CreateServices(Path.Combine(fixture.Root, "data"));
        var loaded = reopened.GetRequiredService<WorkspaceViewModel>();
        await loaded.LoadAsync();
        Assert.Equal(external ? 1 : 0, loaded.FileChanges.Files.Count);
        var saved = await reopened.GetRequiredService<IWorkspaceRepository>().GetMessagesAsync(fixture.TaskId, fixture.Token);
        var inverse = Assert.Single(saved, message => message.ToolCallId?.StartsWith("undo-", StringComparison.Ordinal) == true);
        Assert.Equal(external ? "file_changed" : null, WorkspaceConversationMapper.ToChatMessage(inverse).Tool!.ErrorCode);
        Assert.Equal(requestCount, fixture.Server.Requests.Count);
    }

    [Fact]
    public async Task CancellingConfirmation_DoesNotExecuteAnyTool()
    {
        await using var fixture = await EditedAsync();
        var undo = fixture.Workspace.FileChanges.Undo;
        undo.RequestCommand.Execute(null);
        undo.CancelCommand.Execute(null);

        Assert.False(undo.IsConfirming);
        Assert.False(undo.ConfirmCommand.CanExecute(null));
        Assert.Equal("beta\n", await File.ReadAllTextAsync(fixture.FilePath, fixture.Token));
        Assert.DoesNotContain(fixture.Workspace.Messages, message => message.ToolCallId?.StartsWith("undo-", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task CurrentReadOnlySelection_DisablesUndoEvenForAPreviouslyWritableTask()
    {
        await using var fixture = await EditedAsync();
        fixture.Workspace.SetApprovalPolicyCommand.Execute(ApprovalPolicy.ReadOnly);
        var undo = fixture.Workspace.FileChanges.Undo;
        Assert.False(undo.RequestCommand.CanExecute(null));
        undo.RequestCommand.Execute(null);
        await undo.ConfirmCommand.ExecuteAsync(null);
        Assert.Equal("beta\n", await File.ReadAllTextAsync(fixture.FilePath, fixture.Token));
        Assert.False(undo.IsConfirming);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingApproval_CancelOrSwitchTaskNeverMutatesFilesOrPublishesIntoAnotherTask(bool switchTask)
    {
        await using var fixture = await EditedAsync();
        fixture.Workspace.SetApprovalPolicyCommand.Execute(ApprovalPolicy.AskEveryTime);
        var taskId = fixture.TaskId;
        var undo = fixture.Workspace.FileChanges.Undo;
        undo.RequestCommand.Execute(null);
        var operation = undo.ConfirmCommand.ExecuteAsync(null);
        await fixture.WaitForApprovalAsync();
        Assert.True(undo.IsBusy);
        Assert.True(fixture.Workspace.Catalog.IsTaskRunning!(taskId));
        if (switchTask) fixture.Workspace.Catalog.NewTaskCommand.Execute(null);
        else undo.CancelCommand.Execute(null);
        await operation.WaitAsync(fixture.Token);

        Assert.False(fixture.Approval.IsPending);
        Assert.Equal("beta\n", await File.ReadAllTextAsync(fixture.FilePath, fixture.Token));
        var history = await fixture.Services.GetRequiredService<IWorkspaceRepository>().GetMessagesAsync(taskId, fixture.Token);
        var saved = Assert.Single(history, message => message.ToolCallId?.StartsWith("undo-", StringComparison.Ordinal) == true);
        Assert.Equal("tool_cancelled", WorkspaceConversationMapper.ToChatMessage(saved).Tool!.ErrorCode);
        if (switchTask)
        {
            Assert.Null(fixture.Workspace.Catalog.SelectedTask);
            Assert.Empty(fixture.Workspace.Messages);
            Assert.False(undo.HasNotice);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FullAccessCreatedFileRemoval_StillRequiresRealHighRiskApproval(bool approve)
    {
        await using var fixture = await EditedAsync();
        var createdPath = Path.Combine(fixture.ProjectRoot, "created.txt");
        var context = fixture.Services.GetRequiredService<WorkspaceTaskPanelCoordinator>().CreateContext(
            fixture.Workspace.Catalog.SelectedTask!, fixture.Workspace.Catalog.SelectedProject!);
        var request = new ToolRequest("created-for-undo", "filesystem.write_file",
            JsonSerializer.SerializeToElement(new { path = createdPath, content = "created sentinel" }));
        var result = await fixture.Services.GetRequiredService<IToolExecutor>().ExecuteAsync(request, context, fixture.Token);
        Assert.True(result.IsSuccess, result.Summary);
        var message = new ConversationMessage(Guid.NewGuid(), fixture.TaskId, MessageRole.Tool,
            WorkspaceToolMessageSerializer.Serialize(new AgentToolEvent(request.ToolName, request.CallId, result)), DateTimeOffset.UtcNow, request.CallId);
        await fixture.Services.GetRequiredService<IWorkspaceRepository>().AppendMessageAsync(message, fixture.Token);
        fixture.Workspace.Messages.Add(WorkspaceConversationMapper.ToChatMessage(message));
        var undo = fixture.Workspace.FileChanges.Undo;
        undo.RequestCommand.Execute(null);
        var operation = undo.ConfirmCommand.ExecuteAsync(null);
        await fixture.WaitForApprovalAsync();
        Assert.Equal("filesystem.remove_created_file", fixture.Approval.ToolName);
        if (approve) fixture.Approval.ApproveCommand.Execute(fixture.Approval.CurrentRequestId);
        else fixture.Approval.DenyCommand.Execute(fixture.Approval.CurrentRequestId);
        await operation.WaitAsync(fixture.Token);

        Assert.Equal(!approve, File.Exists(createdPath));
        Assert.Equal("alpha\n", await File.ReadAllTextAsync(fixture.FilePath, fixture.Token));
        var audits = await fixture.Services.GetRequiredService<IToolExecutionAuditReader>().GetRecentAsync(fixture.TaskId, 20, fixture.Token);
        var deletion = Assert.Single(audits, entry => entry.ToolName == "filesystem.remove_created_file");
        Assert.Equal(ToolRiskLevel.HighRisk, deletion.RiskLevel);
        Assert.Equal(ApprovalPolicy.AskEveryTime, deletion.ApprovalPolicy);
        Assert.Equal(approve, deletion.ApprovalGranted);
    }

    private static async Task<WorkspaceFileEditingFixture> EditedAsync()
    {
        var fixture = await WorkspaceFileEditingFixture.CreateAsync(false, "beta\n");
        fixture.Start(ApprovalPolicy.FullAccess);
        await fixture.WaitForCompletionAsync();
        await fixture.AssertPersistedAsync(null, true, AgentTaskStatus.Completed);
        return fixture;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IncompleteOrMissingSnapshotHistory_DisablesConfirmation(bool incomplete)
    {
        await using var fixture = await EditedAsync();
        var row = Assert.Single(fixture.Workspace.Messages, message => message.Tool?.FileChanges is { Count: > 0 });
        var payload = row.Tool!;
        row.UpdateTool(payload with
        {
            FileChangesTruncated = incomplete,
            FileChanges = incomplete ? payload.FileChanges : [payload.FileChanges![0] with { BeforeSnapshot = null }]
        }, false);

        Assert.False(fixture.Workspace.FileChanges.Undo.RequestCommand.CanExecute(null));
        Assert.Equal("beta\n", await File.ReadAllTextAsync(fixture.FilePath, fixture.Token));
    }

    [Fact]
    public async Task ReadOnlyChangeWhileAwaitingApproval_CancelsOwnedOperation()
    {
        await using var fixture = await EditedAsync();
        fixture.Workspace.SetApprovalPolicyCommand.Execute(ApprovalPolicy.AskEveryTime);
        var undo = fixture.Workspace.FileChanges.Undo;
        undo.RequestCommand.Execute(null);
        var operation = undo.ConfirmCommand.ExecuteAsync(null);
        await fixture.WaitForApprovalAsync();
        fixture.Workspace.SetApprovalPolicyCommand.Execute(ApprovalPolicy.ReadOnly);
        await operation.WaitAsync(fixture.Token);

        Assert.False(fixture.Approval.IsPending);
        Assert.False(undo.RequestCommand.CanExecute(null));
        Assert.Equal("beta\n", await File.ReadAllTextAsync(fixture.FilePath, fixture.Token));
    }
}
