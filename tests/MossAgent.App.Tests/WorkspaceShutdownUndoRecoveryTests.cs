using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.Services;
using MossAgent.App.ViewModels;
using MossAgent.Application.Agent;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceShutdownUndoRecoveryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SqliteRecovery_ResavesTheCommittedInverseWithoutReplayingTheTool(bool failAgain)
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        var workspace = fixture.Workspace;
        await workspace.LoadAsync();
        workspace.SetApprovalPolicyCommand.Execute(ApprovalPolicy.FullAccess);
        var path = Path.Combine(fixture.Project.PrimaryDirectory, "sample.cs");
        await File.WriteAllTextAsync(path, "alpha\n", fixture.Token);
        var context = fixture.Panels.CreateContext(fixture.Task, fixture.Project) with { ApprovalPolicy = ApprovalPolicy.FullAccess };
        var request = new ToolRequest("write-before-shutdown", "filesystem.write_file",
            JsonSerializer.SerializeToElement(new { path, content = "beta\n" }));
        var result = await fixture.Tools.ExecuteAsync(request, context, fixture.Token);
        Assert.True(result.IsSuccess, result.Summary);
        var message = new ConversationMessage(Guid.NewGuid(), fixture.Task.Id, MessageRole.Tool,
            WorkspaceToolMessageSerializer.Serialize(new AgentToolEvent(request.ToolName, request.CallId, result)),
            DateTimeOffset.UtcNow, request.CallId);
        await fixture.Repository.AppendMessageAsync(message, fixture.Token);
        workspace.Messages.Add(WorkspaceConversationMapper.ToChatMessage(message));
        await fixture.RejectToolMessageAsync();
        var undo = workspace.FileChanges.Undo;
        undo.RequestCommand.Execute(null);
        await undo.ConfirmCommand.ExecuteAsync(null).WaitAsync(fixture.Token);
        Assert.True(undo.HasUnsavedRecord);
        Assert.Equal("alpha\n", await File.ReadAllTextAsync(path, fixture.Token));
        var callId = Assert.Single(workspace.Messages, item => item.ToolCallId?.StartsWith("undo-", StringComparison.Ordinal) == true).ToolCallId;
        var first = workspace.ShutdownAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => first.WaitAsync(fixture.Token));
        if (failAgain)
        {
            var retry = workspace.ShutdownAsync();
            Assert.NotSame(first, retry);
            await Assert.ThrowsAsync<InvalidOperationException>(() => retry.WaitAsync(fixture.Token));
            Assert.True(undo.HasUnsavedRecord);
        }

        await AllowMessagesAsync(fixture);
        var recovered = workspace.ShutdownAsync();
        await recovered.WaitAsync(fixture.Token);

        Assert.Same(recovered, workspace.ShutdownAsync());
        Assert.False(undo.HasUnsavedRecord);
        Assert.True(workspace.IsShuttingDown);
        Assert.True(workspace.Catalog.IsLocked);
        Assert.False(workspace.PrimaryActionCommand.CanExecute(null));
        Assert.Equal("alpha\n", await File.ReadAllTextAsync(path, fixture.Token));
        var saved = await fixture.Repository.GetMessagesAsync(fixture.Task.Id, fixture.Token);
        Assert.Single(saved, item => item.ToolCallId == callId);
        var audit = await fixture.Audit.GetRecentAsync(fixture.Task.Id, 20, fixture.Token);
        Assert.Single(audit, item => item.ToolName == "filesystem.restore_change");
        await using var reopened = AppComposition.CreateServices(fixture.Project.PrimaryDirectory);
        var loaded = reopened.GetRequiredService<WorkspaceViewModel>();
        await loaded.LoadAsync();
        Assert.Empty(loaded.FileChanges.Files);
        Assert.Equal("alpha\n", await File.ReadAllTextAsync(path, fixture.Token));
    }

    private static async Task AllowMessagesAsync(WorkspacePersistenceFixture fixture)
    {
        await using var connection = new SqliteConnectionFactory(new AppDataPaths(fixture.Project.PrimaryDirectory)).Create();
        await connection.OpenAsync(fixture.Token);
        await using var command = connection.CreateCommand();
        command.CommandText = "DROP TRIGGER reject_tool";
        await command.ExecuteNonQueryAsync(fixture.Token);
    }
}
