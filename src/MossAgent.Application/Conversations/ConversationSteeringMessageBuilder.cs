using System.Collections.Immutable;
using MossAgent.Application.Attachments;
using MossAgent.Application.Models;
using MossAgent.Domain;

namespace MossAgent.Application.Conversations;

/// <summary>
/// Builds the model messages produced by steering inputs that are about to reach the model.
/// Attachment snapshots are read and hash-verified before the boundary transaction commits, so a
/// missing or tampered snapshot fails the boundary instead of silently dropping the user's files.
/// </summary>
internal static class ConversationSteeringMessageBuilder
{
    /// <summary>
    /// Builds one user message per input, appending the text attachment context block and binary image parts.
    /// </summary>
    /// <param name="taskId">Task that owns the snapshots; must match every input.</param>
    /// <param name="inputs">Pending inputs in acceptance order.</param>
    /// <param name="attachments">Attachment store used to read snapshots; required when any input owns attachments.</param>
    /// <param name="cancellationToken">Boundary cancellation token.</param>
    /// <exception cref="ConversationSteeringException">
    /// Thrown with <c>SteeringAttachmentsUnavailable</c> when a snapshot cannot be read or no longer matches its hash.
    /// </exception>
    public static async Task<IReadOnlyList<ModelMessage>> BuildAsync(Guid taskId,
        IReadOnlyList<ConversationSteeringInput> inputs, IConversationAttachmentStore? attachments,
        CancellationToken cancellationToken)
    {
        var messages = new List<ModelMessage>(inputs.Count);
        foreach (var input in inputs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (input.TaskId != taskId) throw new ConversationSteeringException("SteeringScopeChanged");
            var items = input.AttachmentItems;
            if (items.Count == 0)
            {
                messages.Add(new ModelMessage(ModelRole.User, input.Text));
                continue;
            }
            messages.Add(await BuildAsync(taskId, input, items, attachments, cancellationToken));
        }
        return messages;
    }

    private static async Task<ModelMessage> BuildAsync(Guid taskId, ConversationSteeringInput input,
        IReadOnlyList<ConversationAttachment> items, IConversationAttachmentStore? attachments, CancellationToken cancellationToken)
    {
        if (attachments is null) throw new ConversationSteeringException("SteeringAttachmentsUnavailable");
        IReadOnlyList<WorkspaceAttachmentContent> contents;
        try
        {
            contents = await attachments.ReadAsync(taskId, input.Id, items, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested
            && exception is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException
                or TimeoutException or System.Text.DecoderFallbackException)
        {
            throw new ConversationSteeringException("SteeringAttachmentsUnavailable");
        }
        if (contents.Count != items.Count) throw new ConversationSteeringException("SteeringAttachmentsUnavailable");
        var context = WorkspaceAttachmentContextFormatter.Format(contents);
        var images = contents.Where(content => content.Image is not null)
            .Select(content => ModelImage.FromAttachment(content.Image!)).ToImmutableArray();
        return new ModelMessage(ModelRole.User, context is null ? input.Text : input.Text + context,
            Images: images.IsEmpty ? default : images);
    }
}
