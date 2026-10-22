using MossAgent.Application.Attachments;
using MossAgent.Domain;

namespace MossAgent.Application.Persistence;

public static class ConversationFollowupValidation
{
    public static void Validate(ConversationFollowupQueue queue)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(queue.Items);
        if (queue.Items.Any(item => item is null)) throw new ArgumentException("Invalid follow-up queue item.");
        if (queue.TaskId == Guid.Empty || queue.Revision < 0 || queue.Revision == long.MaxValue
            || queue.Items.Count > ConversationFollowupQueue.MaximumItems
            || queue.Items.Select(item => item.Id).Distinct().Count() != queue.Items.Count)
            throw new ArgumentException("Invalid follow-up queue identity, revision or count.");
        foreach (var item in queue.Items) Validate(queue.TaskId, item);
        if (queue.Items.Select(item => (item.ProjectId, item.WorktreePath)).Distinct().Count() > 1)
            throw new ArgumentException("A follow-up queue cannot mix task environments.");
    }

    private static void Validate(Guid taskId, ConversationFollowup item)
    {
        if (item.Model is null || item.Attachments is null || item.AuthorizedRoots is null)
            throw new ArgumentException("Incomplete follow-up snapshot.");
        if (item.Id == Guid.Empty || item.TaskId != taskId || item.ProjectId == Guid.Empty || item.AgentProfileId == Guid.Empty
            || !Enum.IsDefined(item.ApprovalPolicy) || item.CreatedAt == default
            || item.Prompt is null || item.Prompt.Length > ConversationDraft.MaximumTextLength
            || string.IsNullOrWhiteSpace(item.Prompt) && !item.Attachments.Any(attachment => attachment.MediaType != "text/plain")
            || item.AgentInstructions?.Length > ConversationDraft.MaximumTextLength)
            throw new ArgumentException("Invalid follow-up message.");
        if (item.AuthorizedRoots.Count > 64 || item.AuthorizedRoots.Any(root => string.IsNullOrWhiteSpace(root)
                || root.Length > 32767 || root.Contains('\0')) || item.WorktreePath?.Contains('\0') == true
            || item.ProjectId is null && (item.AuthorizedRoots.Count != 0 || item.WorktreePath is not null
                || item.ApprovalPolicy != ApprovalPolicy.ReadOnly))
            throw new UnauthorizedAccessException("Invalid follow-up authorization scope.");
        var model = item.Model;
        if (model.Id == Guid.Empty || model.ProviderId == Guid.Empty || string.IsNullOrWhiteSpace(model.ModelId)
            || model.ModelId.Length > 1024 || model.ReasoningEffort is null || model.ReasoningEffort.Length > 256
            || model.AdvancedParametersJson is null || model.AdvancedParametersJson.Length > 32768
            || model.ContextLength <= 0 || model.MaximumOutputTokens <= 0)
            throw new ArgumentException("Invalid follow-up model snapshot.");
        ConversationAttachmentValidation.Validate(taskId, item.Id, item.Attachments);
        if (!model.SupportsImages
            && item.Attachments.Any(attachment => AttachmentImage.IsSupportedMediaType(attachment.MediaType)))
            throw new ArgumentException("The follow-up model does not support image inputs.");
        if (!model.SupportsFiles
            && item.Attachments.Any(attachment => AttachmentDocument.IsSupportedMediaType(attachment.MediaType)))
            throw new ArgumentException("The follow-up model does not support file inputs.");
    }
}
