using System.Text.Json;
using MossAgent.App.ViewModels;
using MossAgent.Application.Agent;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;

namespace MossAgent.App.Services;

public sealed class WorkspaceFileChangeUndoService(IToolExecutor tools, IWorkspaceRepository workspaces)
{
    public async Task<WorkspaceFileUndoOutcome> ExecuteAsync(WorkspaceFileUndoPlan plan,
        Func<ToolFileChange, Task<bool>> canContinue, Func<ConversationMessage, Task> publish,
        CancellationToken cancellationToken)
    {
        if (plan.Task.Id != plan.Context.TaskId || plan.Task.ProjectId != plan.Project.Id)
            return new("FileUndoFailed");
        try
        {
            foreach (var change in plan.Changes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!await canContinue(change).ConfigureAwait(false)) return new("FileUndoStopped");
                var remove = change.BeforeSha256 is null;
                var name = remove ? "filesystem.remove_created_file" : "filesystem.restore_change";
                var context = plan.Context with
                {
                    ReviewedChange = change,
                    MaximumOutputCharacters = 64 * 1024,
                    ApprovalPolicy = remove && plan.Context.ApprovalPolicy == ApprovalPolicy.FullAccess
                        ? ApprovalPolicy.AskEveryTime : plan.Context.ApprovalPolicy
                };
                var callId = $"undo-{Guid.NewGuid():N}";
                var request = new ToolRequest(callId, name, JsonSerializer.SerializeToElement(new { change }));
                var result = await tools.ExecuteAsync(request, context, cancellationToken).ConfigureAwait(false);
                var message = new ConversationMessage(Guid.NewGuid(), plan.Task.Id, MessageRole.Tool,
                    WorkspaceToolMessageSerializer.Serialize(new AgentToolEvent(name, callId, result)), DateTimeOffset.UtcNow, callId);
                var saved = await SaveAsync(message, false).ConfigureAwait(false);
                await publish(message).ConfigureAwait(false);
                if (!saved) return new("FileUndoSaveFailed", UnsavedRecord: message);
                if (!result.IsSuccess || result.FileChangesTruncated)
                    return new("FileUndoStopped", result.Summary);
            }
            return new("FileUndoCompleted");
        }
        catch (OperationCanceledException) { return new("FileUndoStopped"); }
        catch { return new("FileUndoFailed"); }
    }

    public async Task<bool> SaveAsync(ConversationMessage message, bool checkExisting = true)
    {
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            if (checkExisting)
            {
                var history = await workspaces.GetMessagesAsync(message.TaskId, cleanup.Token).ConfigureAwait(false);
                var existing = history.FirstOrDefault(candidate => candidate.Id == message.Id);
                if (existing is not null) return existing == message;
            }
            await workspaces.AppendMessageAsync(message, cleanup.Token).ConfigureAwait(false);
            return true;
        }
        catch { return false; }
    }
}
