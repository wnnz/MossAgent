using MossAgent.Application.Agent;
using MossAgent.Application.Attachments;
using MossAgent.Application.Models;
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
    private readonly IConversationAttachmentStore? _attachments;
    private readonly ModelProfile? _model;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private int _stopping;

    private ConversationSteeringSession(IConversationSteeringRepository repository, ConversationSteeringRun run,
        CancellationToken runCancellation, Func<CancellationToken, Task>? validateAccess, ConversationSteeringAuthorization? authorization,
        Func<CancellationToken, Task>? validateEmptyBoundary, IConversationAttachmentStore? attachments, ModelProfile? model)
    {
        _repository = repository; _runCancellation = runCancellation;
        _validateAccess = validateAccess;
        _validateEmptyBoundary = validateEmptyBoundary;
        _authorization = authorization;
        _attachments = attachments;
        _model = model;
        TaskId = run.TaskId; RunId = run.Id;
    }

    public Guid TaskId { get; }
    public Guid RunId { get; }
    public bool IsAccepting => Volatile.Read(ref _stopping) == 0 && !_runCancellation.IsCancellationRequested;
    public bool SupportsDrafts(IConversationDraftRepository drafts) => _repository.SupportsDrafts(drafts);

    public static async Task<ConversationSteeringSession?> OpenAsync(IConversationSteeringRepository repository,
        AgentTask task, CancellationToken runCancellation, CancellationToken cancellationToken,
        Func<CancellationToken, Task>? validateAccess = null, ConversationSteeringAuthorization? authorization = null,
        Func<CancellationToken, Task>? validateEmptyBoundary = null, IConversationAttachmentStore? attachments = null,
        ModelProfile? model = null)
    {
        runCancellation.ThrowIfCancellationRequested();
        if (authorization?.Project is { } project)
            authorization = authorization with { Project = project with { AuthorizedDirectories = project.AuthorizedDirectories.ToArray() } };
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, runCancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        if (validateAccess is not null) await validateAccess(deadline.Token);
        var run = await repository.OpenAsync(task, Guid.NewGuid(), deadline.Token, authorization);
        return run is null ? null
            : new(repository, run, runCancellation, validateAccess, authorization, validateEmptyBoundary, attachments, model);
    }

    /// <summary>
    /// Accepts one steering input, optionally together with task-owned attachment snapshots.
    /// </summary>
    /// <param name="expectedRunId">Run identity captured when the draft was bound to this session.</param>
    /// <param name="inputId">Idempotent steering identity; also the snapshot owner id.</param>
    /// <param name="text">Steering text validated by <see cref="ConversationSteeringInput.ValidateText"/>.</param>
    /// <param name="draft">Draft consumption committed in the same transaction, or null when no draft is owned.</param>
    /// <param name="cancellationToken">Caller cancellation token.</param>
    /// <param name="attachments">Attachment snapshots already written for <paramref name="inputId"/>.</param>
    /// <param name="draftAttachmentIds">Composer attachment ids consumed by this input; must match the snapshots.</param>
    /// <exception cref="ConversationSteeringException">
    /// Thrown when the run stopped accepting input, the snapshots are invalid for this input or model, or the store refused them.
    /// </exception>
    public async Task<ConversationSteeringAcceptance> AcceptAsync(Guid expectedRunId, Guid inputId, string text,
        ConversationDraftUpdate? draft, CancellationToken cancellationToken,
        IReadOnlyList<ConversationAttachment>? attachments = null, IReadOnlyCollection<Guid>? draftAttachmentIds = null)
    {
        if (expectedRunId != RunId || !IsAccepting) throw Unavailable();
        var snapshots = attachments?.ToArray() ?? [];
        ValidateAttachments(inputId, snapshots, draft, draftAttachmentIds);
        using var deadline = CreateDeadline(cancellationToken, includeRun: true);
        await _gate.WaitAsync(deadline.Token);
        try
        {
            if (!IsAccepting) throw Unavailable();
            if (_validateAccess is not null) await _validateAccess(deadline.Token);
            deadline.Token.ThrowIfCancellationRequested();
            if (!IsAccepting) throw Unavailable();
            return await _repository.AcceptAsync(TaskId, RunId, inputId, text, draft, deadline.Token, _authorization,
                    snapshots, draftAttachmentIds)
                ?? throw new ConversationSteeringException("SteeringRefused");
        }
        finally { _gate.Release(); }
    }

    public async Task<AgentSteeringApplication?> ApplyAtBoundaryAsync(string? precedingAssistant, bool completeIfEmpty,
        Action<IReadOnlyList<ModelMessage>> validate, CancellationToken cancellationToken)
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
                var prepared = await ConversationSteeringMessageBuilder.BuildAsync(TaskId, pending.Inputs, _attachments, deadline.Token);
                validate(prepared);
                var assistant = string.IsNullOrWhiteSpace(precedingAssistant) ? null
                    : new ConversationMessage(Guid.NewGuid(), TaskId, MessageRole.Assistant, precedingAssistant, DateTimeOffset.UtcNow);
                var applied = await _repository.ApplyAsync(pending.Run, pending.Inputs.Select(input => input.Id).ToArray(), assistant, deadline.Token, _authorization);
                if (applied is not null) return new(applied, prepared);
                var current = await ReadPendingAsync(deadline.Token);
                if (current.Run.Revision == pending.Run.Revision) throw Unavailable();
            }
        }
        finally { _gate.Release(); }
    }

    /// <summary>
    /// Rejects attachment snapshots that cannot belong to this run before the accept transaction starts.
    /// </summary>
    /// <param name="inputId">Snapshot owner id.</param>
    /// <param name="snapshots">Candidate snapshots; empty for text-only steering.</param>
    /// <param name="draft">Draft update that consumes the composer attachments in the same transaction.</param>
    /// <param name="draftAttachmentIds">Composer attachment ids claimed as consumed.</param>
    private void ValidateAttachments(Guid inputId, IReadOnlyList<ConversationAttachment> snapshots,
        ConversationDraftUpdate? draft, IReadOnlyCollection<Guid>? draftAttachmentIds)
    {
        if (snapshots.Count == 0)
        {
            if (draftAttachmentIds is { Count: > 0 }) throw new ConversationSteeringException("SteeringInvalid");
            return;
        }
        if (draft is null || draftAttachmentIds is null || draftAttachmentIds.Count != snapshots.Count)
            throw new ConversationSteeringException("SteeringInvalid");
        if (snapshots.Any(snapshot => snapshot.MediaType != "text/plain") && _model is not { SupportsImages: true })
            throw new ConversationSteeringException("SteeringAttachmentModelUnsupported");
        try { ConversationAttachmentValidation.Validate(TaskId, inputId, snapshots); }
        catch (Exception exception) when (exception is InvalidDataException or UnauthorizedAccessException or ArgumentException)
        {
            throw new ConversationSteeringException("SteeringInvalid");
        }
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
