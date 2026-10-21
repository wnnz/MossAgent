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

public sealed class CopyHandleAgentFlowTests
{
    [Theory]
    [InlineData("file", true, false)]
    [InlineData("file", false, false)]
    [InlineData("file", true, true)]
    [InlineData("tree", true, false)]
    [InlineData("directory", true, false)]
    [InlineData("directory", false, false)]
    [InlineData("directory", true, true)]
    public async Task SavedModelApprovalNativeCompositionAndReloadKeepRealResult(string operation, bool approve, bool refuse)
    {
        var trees = new TrackedFileTreeSessions(refuse);
        var directories = new TrackedDirectoryCreator(refuse);
        var writes = new TrackedAtomicWriteSessions(false);
        await using var fixture = await WorkspaceFileEditingFixture.CreateAsync(false, "", configure: services =>
        {
            Assert.Equal(typeof(WindowsFileTreeReadSessionFactory), Assert.Single(services,
                item => item.ServiceType == typeof(IFileTreeReadSessionFactory)).ImplementationType);
            Assert.Equal(typeof(WindowsAuthorizedDirectoryCreator), Assert.Single(services,
                item => item.ServiceType == typeof(IAuthorizedDirectoryCreator)).ImplementationType);
            services.Replace(ServiceDescriptor.Singleton<IFileTreeReadSessionFactory>(trees));
            services.Replace(ServiceDescriptor.Singleton<IAuthorizedDirectoryCreator>(directories));
            services.Replace(ServiceDescriptor.Singleton<IAtomicFileWriteSessionFactory>(writes));
        }, mutationFactory: (root, file) => CreateMutation(operation, root, file));
        fixture.Start(ApprovalPolicy.AskEveryTime);
        await fixture.WaitForApprovalAsync();
        var toolName = operation == "directory" ? "filesystem.create_directory" : "filesystem.copy_path";
        Assert.Equal(toolName, fixture.Approval.ToolName);
        Assert.Equal(0, trees.Creates + directories.Calls + writes.Creates);
        if (approve) fixture.Approval.ApproveCommand.Execute(fixture.Approval.CurrentRequestId);
        else fixture.Approval.DenyCommand.Execute(fixture.Approval.CurrentRequestId);
        await fixture.WaitForCompletionAsync();
        var success = approve && !refuse;
        var error = !approve ? "approval_denied" : refuse ? operation == "directory" ? "directory_create_unsafe" : "file_tree_unsafe" : null;
        Assert.Equal(operation == "directory" ? 0 : approve ? 1 : 0, trees.Creates);
        Assert.Equal(success && operation != "directory" ? operation == "tree" ? 2 : 1 : 0, writes.Creates);
        var target = Path.Combine(fixture.ProjectRoot, "copied");
        Assert.Equal(success, Path.Exists(target));
        if (success && operation == "file") Assert.Equal("alpha\n", await File.ReadAllTextAsync(target, fixture.Token));
        if (success && operation == "tree")
        {
            Assert.Equal("tree data", await File.ReadAllTextAsync(Path.Combine(target, "one.txt"), fixture.Token));
            Assert.True(Directory.Exists(Path.Combine(target, "empty")));
            Assert.True(File.Exists(Path.Combine(target, "two.bin")));
        }
        Assert.Equal("alpha\n", await File.ReadAllTextAsync(fixture.FilePath, fixture.Token));
        Assert.Equal("outside sentinel", await File.ReadAllTextAsync(fixture.OutsidePath, fixture.Token));
        await AssertRecoveredAsync(fixture, toolName, error, approve);
        Assert.Equal(3, fixture.Server.Requests.Count);
    }

    private static FileEditingMutation CreateMutation(string operation, string root, string file)
    {
        var target = Path.Combine(root, "copied");
        if (operation == "directory") return new("filesystem_create_directory__", new { path = target });
        var source = file;
        if (operation == "tree")
        {
            source = Directory.CreateDirectory(Path.Combine(root, "source")).FullName;
            File.WriteAllText(Path.Combine(source, "one.txt"), "tree data");
            File.WriteAllBytes(Path.Combine(source, "two.bin"), [0, 255, 1, 254]);
            Directory.CreateDirectory(Path.Combine(source, "empty"));
        }
        return new("filesystem_copy_path__", new { source, destination = target });
    }

    private static async Task AssertRecoveredAsync(WorkspaceFileEditingFixture fixture, string tool, string? error, bool approved)
    {
        await using var reopened = AppComposition.CreateServices(Path.Combine(fixture.Root, "data"));
        var workspace = reopened.GetRequiredService<WorkspaceViewModel>();
        await workspace.LoadAsync();
        var history = await reopened.GetRequiredService<IWorkspaceRepository>().GetMessagesAsync(fixture.TaskId, fixture.Token);
        var saved = Assert.Single(history.Select(WorkspaceConversationMapper.ToChatMessage), row => row.ToolCallId == "file-edit");
        Assert.Equal(tool, saved.Tool!.ToolName);
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
