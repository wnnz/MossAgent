using MossAgent.Domain;

namespace MossAgent.Application.SourceControl;

public sealed class SourceReviewChatAccess(ISourceReviewChats chats, SourceRepositoryAccess access)
{
    public async Task<SourceReviewChatBinding?> ResolveAsync(AgentTask task, CancellationToken cancellationToken)
    {
        var binding = await chats.GetAsync(task.Id, cancellationToken);
        if (binding is null) return null;
        binding.Validate();
        if (binding.TaskId != task.Id || task.ProjectId is not null || task.WorktreePath is not null || task.ApprovalPolicy != ApprovalPolicy.ReadOnly)
            throw new SourceReviewException("source_scope_denied");
        await ValidateAsync(binding, cancellationToken);
        return binding;
    }

    public async Task ValidateAsync(SourceReviewChatBinding binding, CancellationToken cancellationToken) =>
        _ = await access.ResolveAsync(binding.Scope, cancellationToken);
}
