using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Agent;
using MossAgent.Application.Models;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceSnapshotRestorePortFlowTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ModelEdit_RestorePortAndPersistedInverseHistoryNeverReplayTheModel(bool external)
    {
        await using var fixture = await WorkspaceFileEditingFixture.CreateAsync(false, "beta\n");
        fixture.Start(ApprovalPolicy.FullAccess);
        await fixture.WaitForCompletionAsync();
        await fixture.AssertPersistedAsync(null, true, AgentTaskStatus.Completed);
        var requestCount = fixture.Server.Requests.Count;
        await RestoreAndPersistAsync(fixture, external);

        await using var reopened = AppComposition.CreateServices(Path.Combine(fixture.Root, "data"));
        var workspace = reopened.GetRequiredService<WorkspaceViewModel>();
        await workspace.LoadAsync();

        Assert.Equal(external, workspace.FileChanges.HasChanges);
        Assert.False(workspace.FileChanges.IsIncomplete);
        Assert.Equal(external ? "external sentinel" : "alpha\n", await File.ReadAllTextAsync(fixture.FilePath, fixture.Token));
        var saved = await reopened.GetRequiredService<IWorkspaceRepository>().GetMessagesAsync(fixture.TaskId, fixture.Token);
        var manual = Assert.Single(saved, message => message.ToolCallId == "manual-restore");
        Assert.Equal(ModelRole.ToolObservation, WorkspaceConversationMapper.ToModelMessage(manual).Role);
        Assert.Equal(external ? "file_changed" : null, WorkspaceConversationMapper.ToChatMessage(manual).Tool!.ErrorCode);
        var audit = Assert.Single(await reopened.GetRequiredService<IToolExecutionAuditReader>().GetRecentAsync(fixture.TaskId, 20, fixture.Token),
            entry => entry.CallId == "manual-restore");
        Assert.Equal(!external, audit.IsSuccess);
        Assert.Equal(external ? "file_changed" : null, audit.ErrorCode);
        Assert.Equal(requestCount, fixture.Server.Requests.Count);
    }

    private static async Task RestoreAndPersistAsync(WorkspaceFileEditingFixture fixture, bool external)
    {
        await using var services = AppComposition.CreateServices(Path.Combine(fixture.Root, "data"));
        var workspace = services.GetRequiredService<WorkspaceViewModel>();
        await workspace.LoadAsync();
        var change = Assert.Single(workspace.FileChanges.Files).Change;
        if (external) await File.WriteAllTextAsync(fixture.FilePath, "external sentinel", fixture.Token);
        var context = services.GetRequiredService<WorkspaceTaskPanelCoordinator>().CreateContext(
            workspace.Catalog.SelectedTask!, workspace.Catalog.SelectedProject!)
        with
        {
            ReviewedChange = change
        };
        var request = new ToolRequest("manual-restore", "filesystem.restore_change", JsonSerializer.SerializeToElement(new { change }));
        var result = await services.GetRequiredService<IToolExecutor>().ExecuteAsync(request, context, fixture.Token);
        Assert.Equal(!external, result.IsSuccess);
        Assert.DoesNotContain(services.GetRequiredService<IToolCatalog>().Descriptors,
            descriptor => descriptor.Name is "filesystem.restore_change" or "filesystem.remove_created_file");
        var raw = WorkspaceToolMessageSerializer.Serialize(new AgentToolEvent(request.ToolName, request.CallId, result));
        await services.GetRequiredService<IWorkspaceRepository>().AppendMessageAsync(
            new(Guid.NewGuid(), fixture.TaskId, MessageRole.Tool, raw, DateTimeOffset.UtcNow, request.CallId), fixture.Token);
    }
}
