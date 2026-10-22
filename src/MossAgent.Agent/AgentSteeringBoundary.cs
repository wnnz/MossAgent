using MossAgent.Application.Agent;
using MossAgent.Application.Models;
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
        var application = await request.Steering.ApplyAtBoundaryAsync(assistant?.Content, completeIfEmpty, prepared =>
        {
            required = prepared;
            var proposed = messages.Concat(assistant is null ? [] : new[] { assistant }).Concat(required).ToArray();
            _ = ModelContextWindowBuilder.Build(proposed, descriptors, request.Model, request.PinnedUserContext, required);
        }, token);
        if (application is null) return null;
        if (assistant is not null) messages.Add(assistant);
        messages.AddRange(required);
        return new(application.Applied, required);
    }
}
