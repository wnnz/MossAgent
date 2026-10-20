using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.Application.SourceControl;

public sealed class SourceReviewChatCreator(ISourceReviewChats chats, SourceRepositoryAccess access, IConversationDraftRepository drafts)
{
    public async Task<AgentTask> OpenAsync(SourceReviewChatBinding binding, string title, CancellationToken cancellationToken)
    {
        binding.Validate();
        await access.ResolveAsync(binding.Scope, cancellationToken);
        var task = await chats.OpenAsync(binding, title, cancellationToken);
        await access.ResolveAsync(binding.Scope, cancellationToken);
        return task;
    }

    public async Task<bool> PrepareDraftAsync(AgentTask task, SourceRepositoryScope scope, string text, CancellationToken cancellationToken)
    {
        if (text.Length is 0 or > 16384) throw new SourceReviewException("invalid_arguments");
        var binding = await chats.GetAsync(task.Id, cancellationToken);
        if (binding is null || binding.Scope != scope) throw new SourceReviewException("source_scope_denied");
        await access.ResolveAsync(scope, cancellationToken);
        var draftScope = new ConversationDraftScope(null, task.Id);
        var previous = await drafts.GetAsync(draftScope, cancellationToken);
        if (previous is not null && (previous.Text.Length != 0 || previous.AttachmentItems.Count != 0)) return false;
        var saved = await drafts.SaveAsync([new(draftScope, text, previous?.Revision ?? 0)], cancellationToken);
        await access.ResolveAsync(scope, cancellationToken);
        return saved;
    }
}
