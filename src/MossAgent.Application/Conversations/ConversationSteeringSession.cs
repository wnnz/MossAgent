using MossAgent.Application.Agent;
using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.Application.Conversations;

public sealed class ConversationSteeringSession : IAgentSteeringInput
{
    private readonly IConversationSteeringRepository _repository;
    private readonly CancellationToken _runCancellation;
    private readonly Func<CancellationToken, Task>? _validateAccess;
    private readonly Func<CancellationToken, Task>? _validateEmptyBoundary;
    private readonly ConversationSteeringAuthorization? _authorization;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private int _stopping;

    private ConversationSteeringSession(IConversationSteeringRepository repository, ConversationSteeringRun run,
        CancellationToken runCancellation, Func<CancellationToken, Task>? validateAccess, ConversationSteeringAuthorization? authorization,
        Func<CancellationToken, Task>? validateEmptyBoundary)
    {
        _repository = repository; _runCancellation = runCancellation;
        _validateAccess = validateAccess;
        _validateEmptyBoundary = validateEmptyBoundary;
        _authorization = authorization;
        TaskId = run.TaskId; RunId = run.Id;
    }

    public Guid TaskId { get; }
    public Guid RunId { get; }
    public bool IsAccepting => Volatile.Read(ref _stopping) == 0 && !_runCancellation.IsCancellationRequested;
    public bool SupportsDrafts(IConversationDraftRepository drafts) => _repository.SupportsDrafts(drafts);

    public static async Task<ConversationSteeringSession?> OpenAsync(IConversationSteeringRepository repository,
        AgentTask task, CancellationToken runCancellation, CancellationToken cancellationToken,
        Func<CancellationToken, Task>? validateAccess = null, ConversationSteeringAuthorization? authorization = null,
        Func<CancellationToken, Task>? validateEmptyBoundary = null)
    {
        runCancellation.ThrowIfCancellationRequested();
        if (authorization?.Project is { } project)
            authorization = authorization with { Project = project.Snapshot() };
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, runCancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        if (validateAccess is not null) await validateAccess(deadline.Token);
        var run = await repository.OpenAsync(task, Guid.NewGuid(), deadline.Token, authorization);
        return run is null ? null : new(repository, run, runCancellation, validateAccess, authorization, validateEmptyBoundary);
    }

    public async Task<ConversationSteeringAcceptance> AcceptAsync(Guid expectedRunId, Guid inputId, string text,
        ConversationDraftUpdate? draft, CancellationToken cancellationToken)
    {
        if (expectedRunId != RunId || !IsAccepting) throw Unavailable();
        using var deadline = CreateDeadline(cancellationToken, includeRun: true);
        await _gate.WaitAsync(deadline.Token);
        try
        {
            if (!IsAccepting) throw Unavailable();
            if (_validateAccess is not null) await _validateAccess(deadline.Token);
            deadline.Token.ThrowIfCancellationRequested();
            if (!IsAccepting) throw Unavailable();
            return await _repository.AcceptAsync(TaskId, RunId, inputId, text, draft, deadline.Token, _authorization)
                ?? throw new ConversationSteeringException("SteeringRefused");
        }
        finally { _gate.Release(); }
    }

    public async Task<ConversationSteeringApplication?> ApplyAtBoundaryAsync(string? precedingAssistant, bool completeIfEmpty,
        Action<IReadOnlyList<ConversationSteeringInput>> validate, CancellationToken cancellationToken)
    {
        using var deadline = CreateDeadline(cancellationToken, includeRun: true);
        await _gate.WaitAsync(deadline.Token);
        try
        {
            while (true)
            {
                deadline.Token.ThrowIfCancellationRequested();
                if (!IsAccepting) throw Unavailable();
                var pending = await ReadPendingAsync(deadline.Token);
                var validateAccess = pending.Inputs.Count == 0 ? _validateEmptyBoundary ?? _validateAccess : _validateAccess;
                if (validateAccess is not null) await validateAccess(deadline.Token);
                deadline.Token.ThrowIfCancellationRequested();
                if (!IsAccepting) throw Unavailable();
                if (pending.Inputs.Count == 0)
                {
                    if (!completeIfEmpty) return null;
                    if (!await _repository.CloseAsync(pending.Run, false, deadline.Token)) continue;
                    Volatile.Write(ref _stopping, 1);
                    return null;
                }
                validate(pending.Inputs);
                var assistant = string.IsNullOrWhiteSpace(precedingAssistant) ? null
                    : new ConversationMessage(Guid.NewGuid(), TaskId, MessageRole.Assistant, precedingAssistant, DateTimeOffset.UtcNow);
                var applied = await _repository.ApplyAsync(pending.Run, pending.Inputs.Select(input => input.Id).ToArray(), assistant, deadline.Token, _authorization);
                if (applied is not null) return applied;
                var current = await ReadPendingAsync(deadline.Token);
                if (current.Run.Revision == pending.Run.Revision) throw Unavailable();
            }
        }
        finally { _gate.Release(); }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        Volatile.Write(ref _stopping, 1);
        using var deadline = CreateDeadline(cancellationToken, includeRun: false);
        await _gate.WaitAsync(deadline.Token);
        try
        {
            while (true)
            {
                deadline.Token.ThrowIfCancellationRequested();
                var run = await _repository.GetRunAsync(TaskId, RunId, deadline.Token);
                if (run is null || !run.IsOpen || await _repository.CloseAsync(run, true, deadline.Token)) return;
            }
        }
        finally { _gate.Release(); }
    }

    private async Task<ConversationSteeringPending> ReadPendingAsync(CancellationToken token)
    {
        var pending = await _repository.GetPendingAsync(TaskId, RunId, token);
        return pending is { Run.IsOpen: true } ? pending : throw Unavailable();
    }

    private CancellationTokenSource CreateDeadline(CancellationToken token, bool includeRun)
    {
        var deadline = includeRun ? CancellationTokenSource.CreateLinkedTokenSource(token, _runCancellation)
            : CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        return deadline;
    }

    private static ConversationSteeringException Unavailable() => new("SteeringRunUnavailable");
}
