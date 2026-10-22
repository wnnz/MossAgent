using System.Collections.Immutable;
using MossAgent.Application.Attachments;
using MossAgent.Application.Models;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

internal sealed class WorkspaceConversationHistoryBuilder(IConversationAttachmentStore? attachments)
{
    public async Task<List<ModelMessage>> BuildAsync(AgentTask task, ModelProfile model,
        IReadOnlyList<ConversationMessage> history, CancellationToken cancellationToken)
    {
        if (history.Any(message => message.TaskId != task.Id))
            throw new InvalidOperationException("Conversation history belongs to another task.");
        foreach (var message in history)
        {
            if (message.Attachments is not { Count: > 0 } items) continue;
            if (message.Role != MessageRole.User)
                throw new UnauthorizedAccessException("Only user messages can own attachments.");
            ConversationAttachmentValidation.Validate(task.Id, message.Id, items);
        }
        var messages = history.Select(WorkspaceConversationMapper.ToModelMessage).ToList();
        var budget = Math.Clamp(((long)model.ContextLength - model.MaximumOutputTokens - 512) * 4, 0, 8 * 1024 * 1024);
        var lastUser = messages.FindLastIndex(message => message.Role == ModelRole.User);
        var imageCount = 0;
        long imageBytes = 0;
        var fileCount = 0;
        long fileBytes = 0;
        for (var index = history.Count - 1; index >= 0; index--)
        {
            cancellationToken.ThrowIfCancellationRequested();
            budget -= messages[index].Content.Length + 32;
            var message = history[index];
            if (message.Attachments is not { Count: > 0 } items) continue;
            var images = items.Where(item => AttachmentImage.IsSupportedMediaType(item.MediaType)).ToArray();
            var files = items.Where(item => AttachmentDocument.IsSupportedMediaType(item.MediaType)).ToArray();
            if (index != lastUser && ExceedsBudget(items, budget, imageCount, imageBytes, fileCount, fileBytes))
            {
                messages[index] = messages[index] with
                {
                    Content = messages[index].Content
                        + "\n[Earlier user-selected attachment content omitted because of the model context budget.]"
                };
                continue;
            }
            var store = attachments ?? throw new InvalidOperationException("Attachment persistence is unavailable.");
            var contents = await ReadAsync(store, task.Id, message.Id, items, cancellationToken);
            var context = WorkspaceAttachmentContextFormatter.Format(contents);
            var payloads = contents.Where(content => content.Image is not null).Select(content => ModelImage.FromAttachment(content.Image!)).ToImmutableArray();
            var filePayloads = contents.Where(content => content.Document is not null)
                .Select(content => ModelFile.FromAttachment(
                    Path.GetFileName(content.DisplayPath), content.Document!)).ToImmutableArray();
            imageCount += payloads.Length;
            imageBytes += images.Sum(item => item.ByteCount);
            fileCount += filePayloads.Length;
            fileBytes += files.Sum(item => item.ByteCount);
            budget -= (context?.Length ?? 0) + payloads.Sum(image => image.ContextCharacterCost)
                + filePayloads.Sum(file => file.ContextCharacterCost);
            messages[index] = messages[index] with
            {
                Content = messages[index].Content + context,
                Images = payloads.IsEmpty ? default : payloads,
                Files = filePayloads.IsEmpty ? default : filePayloads
            };
        }
        return messages;
    }

    private static bool ExceedsBudget(IReadOnlyList<ConversationAttachment> items, long budget,
        int imageCount, long imageBytes, int fileCount, long fileBytes)
    {
        var images = items.Where(item => AttachmentImage.IsSupportedMediaType(item.MediaType)).ToArray();
        var files = items.Where(item => AttachmentDocument.IsSupportedMediaType(item.MediaType)).ToArray();
        return items.Sum(EstimateCost) > budget
            || imageCount + images.Length > ModelImageRequestValidation.MaximumImages
            || imageBytes + images.Sum(item => item.ByteCount) > AttachmentImage.MaximumTotalByteCount
            || fileCount + files.Length > ModelFileRequestValidation.MaximumFiles
            || fileBytes + files.Sum(item => item.ByteCount) > ModelFileRequestValidation.MaximumTotalByteCount;
    }

    private static long EstimateCost(ConversationAttachment attachment) => attachment.MediaType == "text/plain"
        ? attachment.ByteCount : ((attachment.ByteCount + 2) / 3 * 4) + 128;

    private static async Task<IReadOnlyList<WorkspaceAttachmentContent>> ReadAsync(IConversationAttachmentStore store,
        Guid taskId, Guid messageId, IReadOnlyList<ConversationAttachment> items, CancellationToken cancellationToken)
    {
        try { return await store.ReadAsync(taskId, messageId, items, cancellationToken).WaitAsync(cancellationToken); }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested
            && exception is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException
                or OperationCanceledException or TimeoutException or System.Text.DecoderFallbackException)
        {
            throw new ConversationAttachmentUnavailableException();
        }
    }
}
