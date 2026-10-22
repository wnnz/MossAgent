using MossAgent.Application.Agent;
using MossAgent.Application.Models;
using MossAgent.Domain;

namespace MossAgent.Agent.Tests;

internal sealed class ProbeAgentSteeringInput(Guid taskId) : IAgentSteeringInput
{
    private readonly List<ConversationSteeringInput> _pending = [];
    public Guid TaskId { get; } = taskId;
    public Guid RunId { get; } = Guid.NewGuid();
    public List<ConversationSteeringApplication> Applications { get; } = [];
    public bool Closed { get; private set; }
    public long Revision { get; private set; }
    public int PendingCount => _pending.Count;

    public void Accept(string text)
    {
        if (Closed) throw new InvalidOperationException("Run closed.");
        _pending.Add(new(Guid.NewGuid(), TaskId, RunId, ++Revision, text, ConversationSteeringInputState.Pending, DateTimeOffset.UtcNow));
    }

    public Task<AgentSteeringApplication?> ApplyAtBoundaryAsync(string? precedingAssistant, bool completeIfEmpty,
        Action<IReadOnlyList<ModelMessage>> validate, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_pending.Count == 0)
        {
            Closed = completeIfEmpty;
            return Task.FromResult<AgentSteeringApplication?>(null);
        }
        var pending = _pending.ToArray();
        var prepared = pending.Select(item => new ModelMessage(ModelRole.User, item.Text)).ToArray();
        validate(prepared);
        var now = DateTimeOffset.UtcNow;
        var application = new ConversationSteeringApplication(new(RunId, TaskId, ++Revision, now),
            pending.Select(item => item with { State = ConversationSteeringInputState.AppliedAtBoundary, AppliedAt = now }).ToArray(),
            pending.Select(item => new ConversationMessage(item.Id, TaskId, MessageRole.User, item.Text, now)).ToArray(),
            string.IsNullOrWhiteSpace(precedingAssistant) ? null : new(Guid.NewGuid(), TaskId, MessageRole.Assistant, precedingAssistant, now));
        _pending.Clear();
        Applications.Add(application);
        return Task.FromResult<AgentSteeringApplication?>(new(application, prepared));
    }
}
