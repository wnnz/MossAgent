using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MossAgent.App.ViewModels;
using MossAgent.Application.Agent;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Platform.Windows.Files;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Files;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class DeleteHandleCompositionTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    public async Task CreatedFileUndoUsesRegisteredSessionAndRealApprovalThenPersistsResult(bool approve, bool refuse, bool addedStream)
    {
        var sessions = new TrackedPathDeleteSessions(refuse);
        await using var fixture = await WorkspaceFileEditingFixture.CreateAsync(false, "beta\n", configure: services =>
        {
            Assert.Equal(typeof(WindowsPathDeleteSessionFactory),
                Assert.Single(services, service => service.ServiceType == typeof(IPathDeleteSessionFactory)).ImplementationType);
            services.Replace(ServiceDescriptor.Singleton<IPathDeleteSessionFactory>(sessions));
        });
        fixture.Start(ApprovalPolicy.FullAccess);
        await fixture.WaitForCompletionAsync();
        var path = Path.Combine(fixture.ProjectRoot, "created.txt");
        await AddCreatedFileAsync(fixture, path);
        if (addedStream) await File.WriteAllTextAsync(path + ":external", "external metadata", fixture.Token);
        var requests = fixture.Server.Requests.Count;
        var undo = fixture.Workspace.FileChanges.Undo;
        undo.RequestCommand.Execute(null);
        var operation = undo.ConfirmCommand.ExecuteAsync(null);
        await fixture.WaitForApprovalAsync();
        Assert.Equal("filesystem.remove_created_file", fixture.Approval.ToolName);
        if (approve) fixture.Approval.ApproveCommand.Execute(fixture.Approval.CurrentRequestId);
        else fixture.Approval.DenyCommand.Execute(fixture.Approval.CurrentRequestId);
        await operation.WaitAsync(fixture.Token);
        Assert.Equal(approve ? 1 : 0, sessions.Creates);
        Assert.Equal(!approve || refuse || addedStream, File.Exists(path));
        var error = !approve ? "approval_denied" : refuse ? "file_delete_unsafe" : addedStream ? "file_delete_metadata_unsupported" : null;
        await AssertRecoveredAsync(fixture, error, approve);
        if (addedStream) Assert.Equal("external metadata", await File.ReadAllTextAsync(path + ":external", fixture.Token));
        Assert.Equal(requests, fixture.Server.Requests.Count);
        Assert.Equal(approve ? 1 : 0, sessions.Creates);
    }

    [Fact]
    public async Task ExplicitDeleteUsesProductionRegistrationAndPersistsAudit()
    {
        var sessions = new TrackedPathDeleteSessions(false);
        await using var fixture = await WorkspaceFileEditingFixture.CreateAsync(false, "beta\n", configure: services =>
            services.Replace(ServiceDescriptor.Singleton<IPathDeleteSessionFactory>(sessions)));
        fixture.Start(ApprovalPolicy.FullAccess);
        await fixture.WaitForCompletionAsync();
        var context = fixture.Services.GetRequiredService<WorkspaceTaskPanelCoordinator>().CreateContext(
            fixture.Workspace.Catalog.SelectedTask!, fixture.Workspace.Catalog.SelectedProject!);
        var request = new ToolRequest("native-delete", "filesystem.delete_path", JsonSerializer.SerializeToElement(new { path = fixture.FilePath }));
        var pending = fixture.Services.GetRequiredService<IToolExecutor>().ExecuteAsync(
            request, context with { ApprovalPolicy = ApprovalPolicy.AskEveryTime }, fixture.Token);
        await fixture.WaitForApprovalAsync();
        Assert.Equal("filesystem.delete_path", fixture.Approval.ToolName);
        Assert.Equal(0, sessions.Creates);
        fixture.Approval.ApproveCommand.Execute(fixture.Approval.CurrentRequestId);
        var result = await pending.WaitAsync(fixture.Token);
        Assert.True(result.IsSuccess, result.Summary);
        Assert.Equal(1, sessions.Creates);
        Assert.False(File.Exists(fixture.FilePath));
        var audits = await fixture.Services.GetRequiredService<IToolExecutionAuditReader>().GetRecentAsync(fixture.TaskId, 20, fixture.Token);
        var deletion = Assert.Single(audits, entry => entry.CallId == "native-delete");
        Assert.True(deletion.ApprovalGranted);
        Assert.True(deletion.IsSuccess);
    }

    private static async Task AddCreatedFileAsync(WorkspaceFileEditingFixture fixture, string path)
    {
        var context = fixture.Services.GetRequiredService<WorkspaceTaskPanelCoordinator>().CreateContext(
            fixture.Workspace.Catalog.SelectedTask!, fixture.Workspace.Catalog.SelectedProject!);
        var request = new ToolRequest("created-for-native-undo", "filesystem.write_file",
            JsonSerializer.SerializeToElement(new { path, content = "created sentinel" }));
        var result = await fixture.Services.GetRequiredService<IToolExecutor>().ExecuteAsync(request, context, fixture.Token);
        Assert.True(result.IsSuccess, result.Summary);
        var message = new ConversationMessage(Guid.NewGuid(), fixture.TaskId, MessageRole.Tool,
            WorkspaceToolMessageSerializer.Serialize(new AgentToolEvent(request.ToolName, request.CallId, result)), DateTimeOffset.UtcNow, request.CallId);
        await fixture.Services.GetRequiredService<IWorkspaceRepository>().AppendMessageAsync(message, fixture.Token);
        fixture.Workspace.Messages.Add(WorkspaceConversationMapper.ToChatMessage(message));
    }

    private static async Task AssertRecoveredAsync(WorkspaceFileEditingFixture fixture, string? error, bool approved)
    {
        await using var reopened = AppComposition.CreateServices(Path.Combine(fixture.Root, "data"));
        var loaded = reopened.GetRequiredService<WorkspaceViewModel>();
        await loaded.LoadAsync();
        var history = await reopened.GetRequiredService<IWorkspaceRepository>().GetMessagesAsync(fixture.TaskId, fixture.Token);
        var deletion = Assert.Single(history.Select(WorkspaceConversationMapper.ToChatMessage),
            row => row.Tool?.ToolName == "filesystem.remove_created_file");
        Assert.Equal(error, deletion.Tool!.ErrorCode);
        Assert.Equal(error is null, deletion.Tool.IsSuccess);
        Assert.Contains(fixture.Workspace.Messages, row => row.Tool?.ToolName == "filesystem.remove_created_file" && row.Tool.ErrorCode == error);
        var audits = await reopened.GetRequiredService<IToolExecutionAuditReader>().GetRecentAsync(fixture.TaskId, 20, fixture.Token);
        var audit = Assert.Single(audits, entry => entry.ToolName == "filesystem.remove_created_file");
        Assert.Equal(approved, audit.ApprovalGranted);
        Assert.Equal(error, audit.ErrorCode);
        Assert.Equal(ToolRiskLevel.HighRisk, audit.RiskLevel);
        Assert.Equal(ApprovalPolicy.AskEveryTime, audit.ApprovalPolicy);
        Assert.NotNull(audit.CompletedAt);
    }
}
