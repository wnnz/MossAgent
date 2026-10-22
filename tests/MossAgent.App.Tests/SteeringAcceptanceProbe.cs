using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.Tests;

internal sealed class SteeringAcceptanceProbe(IConversationSteeringRepository inner) : IConversationSteeringRepository
{
    public Func<CancellationToken, Task>? BeforeAccept { get; set; }
    public Action<ConversationSteeringAcceptance?>? AfterAccept { get; set; }
    public Func<CancellationToken, Task>? BeforeApply { get; set; }
    public Func<CancellationToken, Task>? BeforeRead { get; set; }
    public bool SupportsDrafts(IConversationDraftRepository drafts) => inner.SupportsDrafts(drafts);
    public Task<ConversationSteeringRun?> OpenAsync(AgentTask task, Guid id, CancellationToken token,
        ConversationSteeringAuthorization? authorization = null) => inner.OpenAsync(task, id, token, authorization);
    public Task<ConversationSteeringRun?> GetRunAsync(Guid task, Guid run, CancellationToken token) => inner.GetRunAsync(task, run, token);
    public Task<ConversationSteeringPending?> GetPendingAsync(Guid task, Guid run, CancellationToken token) => inner.GetPendingAsync(task, run, token);
    public async Task<ConversationSteeringPage> GetInputsAsync(Guid task, long before, int size, CancellationToken token)
    {
        if (BeforeRead is not null) await BeforeRead(token);
        return await inner.GetInputsAsync(task, before, size, token);
    }
    public async Task<ConversationSteeringAcceptance?> AcceptAsync(Guid task, Guid run, Guid id, string text,
        ConversationDraftUpdate? draft, CancellationToken token, ConversationSteeringAuthorization? authorization = null,
        IReadOnlyList<ConversationAttachment>? attachments = null, IReadOnlyCollection<Guid>? draftAttachmentIds = null)
    {
        if (BeforeAccept is not null) await BeforeAccept(token);
        var receipt = await inner.AcceptAsync(task, run, id, text, draft, token, authorization, attachments, draftAttachmentIds);
        AfterAccept?.Invoke(receipt);
        return receipt;
    }
    public async Task<ConversationSteeringApplication?> ApplyAsync(ConversationSteeringRun run, IReadOnlyList<Guid> ids,
        ConversationMessage? assistant, CancellationToken token, ConversationSteeringAuthorization? authorization = null)
    {
        if (BeforeApply is not null) await BeforeApply(token);
        return await inner.ApplyAsync(run, ids, assistant, token, authorization);
    }
    public Task<bool> CloseAsync(ConversationSteeringRun run, bool interrupt, CancellationToken token) => inner.CloseAsync(run, interrupt, token);
}
