using System.Collections.Immutable;
using MossAgent.Application.Agent;
using MossAgent.Application.Attachments;
using MossAgent.Application.Models;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;

namespace MossAgent.Agent;

internal static class AgentSteeringBoundary
{
    public static async Task<AgentSteeringBoundaryResult?> ApplyAsync(AgentRunRequest request, List<ModelMessage> messages,
        IReadOnlyList<ToolDescriptor> descriptors, ModelTurnState? completed, bool completeIfEmpty, CancellationToken token)
    {
        if (request.Steering is null) return null;
        if (request.Steering.TaskId != request.ToolContext.TaskId || request.Steering.RunId == Guid.Empty)
            throw new InvalidOperationException("Steering input belongs to another run.");
        var assistant = completed is null ? null : new ModelMessage(ModelRole.Assistant,
            completed.AssistantText.ToString(), Continuation: completed.Continuation);
        IReadOnlyList<ModelMessage> required = [];
        var application = await request.Steering.ApplyAtBoundaryAsync(assistant?.Content, completeIfEmpty, pending =>
        {
            ValidateCapability(request, pending);
            required = pending.Select(input => new ModelMessage(ModelRole.User,
                input.Text + new string(' ', (int)input.AttachmentItems.Sum(EstimateCost)))).ToArray();
            var proposed = messages.Concat(assistant is null ? [] : new[] { assistant }).Concat(required).ToArray();
            _ = ModelContextWindowBuilder.Build(proposed, descriptors, request.Model, request.PinnedUserContext, required);
        }, token);
        if (application is null) return null;
        if (assistant is not null) messages.Add(assistant);
        required = await MaterializeAsync(request, application, token);
        messages.AddRange(required);
        return new(application, required);
    }

    private static long EstimateCost(ConversationAttachment attachment) => attachment.MediaType switch
    {
        "text/plain" => attachment.ByteCount * 2 + attachment.DisplayPath.Length + 96,
        AttachmentDocument.PdfMediaType =>
            Math.Min(attachment.ByteCount, AttachmentDocument.MaximumTextCharacters) * 2 + attachment.DisplayPath.Length + 160,
        _ => ((attachment.ByteCount + 2) / 3 * 4) + 128
    };

    private static void ValidateCapability(AgentRunRequest request, IReadOnlyList<ConversationSteeringInput> pending)
    {
        var images = pending.SelectMany(input => input.AttachmentItems)
            .Where(item => AttachmentImage.IsSupportedMediaType(item.MediaType)).ToArray();
        if (images.Length == 0) return;
        if (!request.Model.SupportsImages || !ModelProtocolCapabilities.AcceptsImages(request.Provider.Protocol))
            throw new ModelCapabilityException(ModelCapabilityException.UnsupportedImageInput,
                "The selected model does not support image inputs. Remove the image attachments "
                + "or switch to a model that supports images.");
        if (images.Length > ModelImageRequestValidation.MaximumImages
            || images.Sum(item => item.ByteCount) > AttachmentImage.MaximumTotalByteCount)
            throw new ArgumentException("Image inputs exceed the request limit.");
    }

    private static async Task<IReadOnlyList<ModelMessage>> MaterializeAsync(AgentRunRequest request,
        ConversationSteeringApplication application, CancellationToken token)
    {
        var required = new List<ModelMessage>(application.Inputs.Count);
        foreach (var input in application.Inputs)
        {
            if (input.AttachmentItems.Count == 0)
            {
                required.Add(new ModelMessage(ModelRole.User, input.Text));
                continue;
            }
            if (request.AttachmentStore is null) throw new InvalidOperationException("Attachment persistence is unavailable.");
            var contents = await ReadAsync(request.AttachmentStore, input.TaskId, input.Id, input.AttachmentItems, token);
            var context = WorkspaceAttachmentContextBuilder.Build(contents);
            var payloads = contents.Where(item => item.Image is not null)
                .Select(item => ModelImage.FromAttachment(item.Image!)).ToImmutableArray();
            required.Add(new ModelMessage(ModelRole.User, input.Text + context,
                Images: payloads.IsEmpty ? default : payloads));
        }
        return required;
    }

    private static async Task<IReadOnlyList<WorkspaceAttachmentContent>> ReadAsync(IConversationAttachmentStore store,
        Guid taskId, Guid inputId, IReadOnlyList<ConversationAttachment> items, CancellationToken token)
    {
        try { return await store.ReadAsync(taskId, inputId, items, token).WaitAsync(token); }
        catch (Exception exception) when (!token.IsCancellationRequested
            && exception is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException
                or OperationCanceledException or TimeoutException or System.Text.DecoderFallbackException)
        {
            throw new ConversationAttachmentUnavailableException();
        }
    }
}
