using System.Collections.Concurrent;
using MossAgent.App.Browser;
using MossAgent.Application.Conversations;
using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

internal sealed class WorkspaceFollowupRuntime : IDisposable
{
    private readonly IConversationFollowupRepository _repository;
    private readonly WorkspaceFollowupEnqueuer _enqueuer;
    private readonly WorkspaceFollowupDispatcher _dispatcher;
    private readonly WorkspaceRunTracker _runs;
    private readonly WorkspaceRunController _controller;
    private readonly WorkspaceFollowupOperations _operations = new();
    private readonly ConcurrentDictionary<Guid, WorkspaceRunState> _armed = new();
    private readonly ConcurrentDictionary<Guid, string> _notices = new();

    public WorkspaceFollowupRuntime(IConversationFollowupRepository repository, WorkspaceFollowupEnqueuer enqueuer,
        WorkspaceFollowupDispatcher dispatcher, WorkspaceRunTracker runs, WorkspaceRunController controller)
    {
        _repository = repository; _enqueuer = enqueuer; _dispatcher = dispatcher; _runs = runs; _controller = controller;
        controller.RunFinished += HandleFinishedAsync;
        _operations.StateChanged += HandleStateChangedAsync;
    }

    public event Action<Guid, ConversationFollowupQueue?, string?>? Changed;
    public event Action? StateChanged;
    public bool IsPreparing(Guid taskId) => _operations.IsPreparing(taskId);
    public void CancelPreparation(Guid taskId) => _operations.CancelPreparation(taskId);
    public string? Notice(Guid taskId) => _notices.GetValueOrDefault(taskId);
    public bool IsArmed(Guid taskId) => _armed.ContainsKey(taskId);
    public Task<ConversationFollowupQueue> LoadAsync(Guid taskId, CancellationToken token) => _repository.GetAsync(taskId, token);

    public async Task EnqueueAsync(WorkspaceSendRequest request)
    {
        var taskId = request.ExistingTask?.Id ?? throw new ConversationFollowupException("FollowupTaskUnavailable");
        if (!_runs.TryGet(taskId, out var captured) || captured is null)
            throw new ConversationFollowupException("FollowupTaskRunning");
        var continueNow = false;
        await _operations.RunAsync(taskId, true, async token =>
        {
            var queue = await _enqueuer.EnqueueAsync(request, token);
            var eligible = !token.IsCancellationRequested && !captured.IsCancellationRequested
                && (queue.Items.Count == 1 || _armed.ContainsKey(taskId));
            if (eligible && (_runs.TryGet(taskId, out var active) && ReferenceEquals(active, captured)
                || !_runs.IsRunning(taskId) && captured.Task.Status == AgentTaskStatus.Completed))
            {
                _armed[taskId] = captured;
                continueNow = !_runs.IsRunning(taskId);
            }
            await PublishAsync(taskId, queue, _armed.ContainsKey(taskId) ? null : "FollowupPaused");
        });
        if (continueNow) await ContinueCoreAsync(taskId, captured);
    }

    public Task ContinueAsync(Guid taskId) => ContinueCoreAsync(taskId, null);

    private async Task HandleFinishedAsync(WorkspaceRunState state, bool released)
    {
        if (!_armed.TryGetValue(state.Task.Id, out var armed) || !ReferenceEquals(armed, state)) return;
        if (!released || state.Task.Status != AgentTaskStatus.Completed || state.IsCancellationRequested || _operations.IsStopping)
        {
            _armed.TryRemove(new(state.Task.Id, state));
            await PublishAsync(state.Task.Id, null, state.AuthorizationChanged ? "FollowupScopeChanged" : "FollowupPaused");
            return;
        }
        await ContinueCoreAsync(state.Task.Id, state);
    }

    private async Task ContinueCoreAsync(Guid taskId, WorkspaceRunState? predecessor)
    {
        if (_operations.IsStopping) return;
        try { await RunContinuationAsync(taskId, predecessor); }
        catch (ObjectDisposedException) when (_operations.IsStopping) { }
    }

    private Task RunContinuationAsync(Guid taskId, WorkspaceRunState? predecessor) =>
        _operations.RunAsync(taskId, true, async token =>
        {
            if (predecessor is not null && (!_armed.TryGetValue(taskId, out var armed) || !ReferenceEquals(armed, predecessor))) return;
            try
            {
                if (_runs.IsRunning(taskId)) throw new ConversationFollowupException("FollowupTaskRunning");
                await _dispatcher.DispatchAsync(taskId, state => _armed[taskId] = state, token);
                // Once launched, follow-up cancellation belongs to that run, not this preparation.
                var queue = await _repository.GetAsync(taskId, CancellationToken.None);
                if (queue.Items.Count == 0) _armed.TryRemove(taskId, out _);
                await PublishAsync(taskId, queue, null);
            }
            catch (Exception exception)
            {
                _armed.TryRemove(taskId, out _);
                await PublishAsync(taskId, null, FailureKey(exception));
            }
        });

    public Task ReplaceAsync(ConversationFollowupQueue expected, IReadOnlyList<ConversationFollowup> items) =>
        _operations.RunAsync(expected.TaskId, false, async token =>
        {
            if (!await _repository.SaveAsync(expected with { Items = items.ToArray() }, [], token))
                throw new ConversationFollowupException("FollowupConflict");
            if (items.Count == 0) _armed.TryRemove(expected.TaskId, out _);
            await PublishAsync(expected.TaskId, expected with { Revision = checked(expected.Revision + 1), Items = items.ToArray() },
                _armed.ContainsKey(expected.TaskId) ? null : "FollowupPaused");
        });

    private Task PublishAsync(Guid taskId, ConversationFollowupQueue? queue, string? notice) => AvaloniaUiThread.RunAsync(() =>
    {
        if (notice is null) _notices.TryRemove(taskId, out _); else _notices[taskId] = notice;
        Changed?.Invoke(taskId, queue, notice);
    }, CancellationToken.None);

    internal static string FailureKey(Exception exception) => exception switch
    {
        ConversationFollowupException failure => failure.Code,
        MossAgent.Application.Models.ModelCapabilityException => "FollowupModelUnsupported",
        OperationCanceledException => "FollowupPaused",
        UnauthorizedAccessException => "FollowupScopeChanged",
        MossAgent.Application.Attachments.ConversationAttachmentUnavailableException => "FollowupAttachmentUnavailable",
        IOException or InvalidDataException => "FollowupAttachmentUnavailable",
        _ => "FollowupFailed"
    };

    private Task HandleStateChangedAsync() => AvaloniaUiThread.RunAsync(() => StateChanged?.Invoke(), CancellationToken.None);
    public Task StopAsync() => _operations.StopAsync();
    public void Dispose()
    {
        _controller.RunFinished -= HandleFinishedAsync;
        _operations.StateChanged -= HandleStateChangedAsync;
        _operations.Dispose();
        _armed.Clear();
    }
}
