using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Files;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class DeleteHandleAgentFlowTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task SavedModelThroughRealAgentApprovalNativeDeleteAndReload(bool approved, bool refused)
    {
        var sessions = new TrackedPathDeleteSessions(refused);
        await using var fixture = await WorkspaceFileEditingFixture.CreateAsync(false, "", configure: services =>
            services.Replace(ServiceDescriptor.Singleton<IPathDeleteSessionFactory>(sessions)), delete: true);
        fixture.Start(ApprovalPolicy.AskEveryTime);
        await fixture.WaitForApprovalAsync();
        Assert.Equal("filesystem.delete_path", fixture.Approval.ToolName);
        Assert.Equal(0, sessions.Creates);
        Assert.True(File.Exists(fixture.FilePath));
        if (approved) fixture.Approval.ApproveCommand.Execute(fixture.Approval.CurrentRequestId);
        else fixture.Approval.DenyCommand.Execute(fixture.Approval.CurrentRequestId);
        await fixture.WaitForCompletionAsync();
        var error = !approved ? "approval_denied" : refused ? "file_delete_unsafe" : null;
        Assert.Equal(approved ? 1 : 0, sessions.Creates);
        Assert.Equal(!approved || refused, File.Exists(fixture.FilePath));
        Assert.Equal("outside sentinel", await File.ReadAllTextAsync(fixture.OutsidePath, fixture.Token));
        Assert.Equal(3, fixture.Server.Requests.Count);
        Assert.All(fixture.Server.Requests, request => Assert.Contains("file-flow-model", request));
        await using var reopened = AppComposition.CreateServices(Path.Combine(fixture.Root, "data"));
        var workspace = reopened.GetRequiredService<WorkspaceViewModel>();
        await workspace.LoadAsync();
        var messages = await reopened.GetRequiredService<IWorkspaceRepository>().GetMessagesAsync(fixture.TaskId, fixture.Token);
        var deletion = Assert.Single(messages.Select(WorkspaceConversationMapper.ToChatMessage), row => row.ToolCallId == "file-edit");
        Assert.Equal("filesystem.delete_path", deletion.Tool!.ToolName);
        Assert.Equal(error, deletion.Tool.ErrorCode);
        Assert.Equal(error is null, deletion.Tool.IsSuccess);
        Assert.Contains(workspace.Messages, row => row.ToolCallId == "file-edit" && row.Tool?.ErrorCode == error);
        var audits = await reopened.GetRequiredService<IToolExecutionAuditReader>().GetRecentAsync(fixture.TaskId, 20, fixture.Token);
        var audit = Assert.Single(audits, row => row.CallId == "file-edit");
        Assert.Equal(approved, audit.ApprovalGranted);
        Assert.Equal(error, audit.ErrorCode);
        Assert.Equal(AgentTaskStatus.Completed, fixture.Workspace.Catalog.SelectedTask!.Status);
        Assert.Equal(3, fixture.Server.Requests.Count);
    }
}
