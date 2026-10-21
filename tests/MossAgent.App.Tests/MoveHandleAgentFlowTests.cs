using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Platform.Windows.Files;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Files;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class MoveHandleAgentFlowTests
{
    [Theory]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public async Task SavedModelApprovalNativeMoveAndReloadRetainRealResult(bool directory, bool approve, bool refuse)
    {
        var moves = new TrackedPathMoveSessions(refuse);
        var directories = new TrackedDirectoryCreator(false);
        await using var fixture = await WorkspaceFileEditingFixture.CreateAsync(false, "", configure: services =>
        {
            Assert.Equal(typeof(WindowsPathMoveSessionFactory), Assert.Single(services,
                item => item.ServiceType == typeof(IPathMoveSessionFactory)).ImplementationType);
            services.Replace(ServiceDescriptor.Singleton<IPathMoveSessionFactory>(moves));
            services.Replace(ServiceDescriptor.Singleton<IAuthorizedDirectoryCreator>(directories));
        }, mutationFactory: (root, file) => CreateMutation(directory, root, file));
        fixture.Start(ApprovalPolicy.AskEveryTime);
        await fixture.WaitForApprovalAsync();
        Assert.Equal("filesystem.move_path", fixture.Approval.ToolName);
        Assert.Equal(0, moves.Creates + directories.Calls);
        if (approve) fixture.Approval.ApproveCommand.Execute(fixture.Approval.CurrentRequestId);
        else fixture.Approval.DenyCommand.Execute(fixture.Approval.CurrentRequestId);
        await fixture.WaitForCompletionAsync();
        var success = approve && !refuse;
        var error = !approve ? "approval_denied" : refuse ? "file_move_unsafe" : null;
        Assert.Equal(approve ? 1 : 0, moves.Creates);
        var target = Path.Combine(fixture.ProjectRoot, "moved");
        Assert.Equal(success, Path.Exists(target));
        if (success)
            Assert.Equal(directory ? "tree data" : "alpha\n",
                await File.ReadAllTextAsync(directory ? Path.Combine(target, "child.txt") : target, fixture.Token));
        Assert.Equal(!success || directory, File.Exists(fixture.FilePath));
        if (directory)
        {
            Assert.Equal(!success, Directory.Exists(Path.Combine(fixture.ProjectRoot, "source")));
            if (success) Assert.True(Directory.Exists(Path.Combine(target, "empty")));
        }
        Assert.Equal("outside sentinel", await File.ReadAllTextAsync(fixture.OutsidePath, fixture.Token));
        await AssertRecoveredAsync(fixture, error, approve);
        Assert.Equal(3, fixture.Server.Requests.Count);
    }

    private static FileEditingMutation CreateMutation(bool directory, string root, string file)
    {
        var source = file;
        if (directory)
        {
            source = Directory.CreateDirectory(Path.Combine(root, "source")).FullName;
            File.WriteAllText(Path.Combine(source, "child.txt"), "tree data");
            Directory.CreateDirectory(Path.Combine(source, "empty"));
        }
        return new("filesystem_move_path__", new { source, destination = Path.Combine(root, "moved") });
    }

    private static async Task AssertRecoveredAsync(WorkspaceFileEditingFixture fixture, string? error, bool approved)
    {
        await using var reopened = AppComposition.CreateServices(Path.Combine(fixture.Root, "data"));
        var workspace = reopened.GetRequiredService<WorkspaceViewModel>();
        await workspace.LoadAsync();
        var history = await reopened.GetRequiredService<IWorkspaceRepository>().GetMessagesAsync(fixture.TaskId, fixture.Token);
        var saved = Assert.Single(history.Select(WorkspaceConversationMapper.ToChatMessage), row => row.ToolCallId == "file-edit");
        Assert.Equal("filesystem.move_path", saved.Tool!.ToolName);
        Assert.Equal(error, saved.Tool.ErrorCode);
        Assert.Equal(error is null, saved.Tool.IsSuccess);
        Assert.Contains(workspace.Messages, row => row.ToolCallId == "file-edit" && row.Tool?.ErrorCode == error);
        var audits = await reopened.GetRequiredService<IToolExecutionAuditReader>().GetRecentAsync(fixture.TaskId, 20, fixture.Token);
        var audit = Assert.Single(audits, entry => entry.CallId == "file-edit");
        Assert.Equal(approved, audit.ApprovalGranted);
        Assert.Equal(error, audit.ErrorCode);
        Assert.Equal(AgentTaskStatus.Completed, fixture.Workspace.Catalog.SelectedTask!.Status);
        Assert.All(fixture.Server.Requests, request => Assert.Contains("file-flow-model", request));
    }
}
