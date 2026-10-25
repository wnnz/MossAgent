using MossAgent.Application.Persistence;

namespace MossAgent.App.ViewModels;

internal sealed class GitCommitDraftBookkeeping : IAsyncDisposable
{
    private static readonly TimeSpan DisposeWait = TimeSpan.FromSeconds(5);
    private readonly IGitCommitDraftRepository? _repository;
    private readonly TimeSpan _saveDelay;
    private readonly object _gate = new();
    private readonly Dictionary<Guid, GitCommitDraftState> _states = [];
    private readonly CancellationTokenSource _shutdown = new();
    private Task _writer = Task.CompletedTask;
    private bool _disposed;

    public GitCommitDraftBookkeeping(IGitCommitDraftRepository? repository, TimeSpan? saveDelay = null)
    {
        _repository = repository;
        _saveDelay = saveDelay ?? TimeSpan.FromMilliseconds(200);
    }

    public string TextOf(Guid taskId)
    {
        lock (_gate) return For(taskId).Text;
    }

    public bool IsLoaded(Guid taskId)
    {
        lock (_gate) return For(taskId).Loaded;
    }

    private GitCommitDraftState For(Guid taskId)
    {
        if (!_states.TryGetValue(taskId, out var state))
            _states[taskId] = state = new() { Loaded = _repository is null };
        return state;
    }

    // Latest session text for a task; schedules a debounced persisted write when it
    // differs from the last known persisted value.
    public void NoteEdited(Guid taskId, string text)
    {
        lock (_gate)
        {
            if (_disposed) return;
            var state = For(taskId);
            state.Text = text;
            state.Edited = true;
            state.Conflicted = false;
        }
        EnsureWriter();
    }

    // Programmatic text change (attach restore, draft consumption): persists the new
    // text but does not set Edited, so a still-running load may not override user input.
    public void NoteSet(Guid taskId, string text)
    {
        lock (_gate)
        {
            if (_disposed) return;
            var state = For(taskId);
            state.Text = text;
            state.Conflicted = false;
        }
        EnsureWriter();
    }

    // Returns the text the caller may apply, or null when the task was edited in the
    // meantime or the load failed (a failed load stays unloadable-quietly and is
    // retried on the next attach).
    public async Task<string?> LoadAsync(Guid taskId, CancellationToken cancellationToken)
    {
        GitCommitDraftState state;
        string before;
        lock (_gate)
        {
            state = For(taskId);
            if (_disposed || state.Loaded || _repository is null) return null;
            before = state.Text;
        }
        GitCommitDraft? draft;
        try { draft = await _repository.GetAsync(taskId, cancellationToken).ConfigureAwait(false); }
        catch (Exception) { return null; }
        lock (_gate)
        {
            state.Loaded = true;
            state.Revision = draft?.Revision ?? 0;
            state.Persisted = draft?.Text ?? string.Empty;
            if (_disposed || state.Edited || state.Text != before) return null;
            state.Text = state.Persisted;
            return state.Text;
        }
    }

    public void EnsureWriter()
    {
        lock (_gate)
        {
            if (!_disposed && _repository is not null && _writer.IsCompleted)
                _writer = Task.Run(DrainAsync);
        }
    }

    private async Task DrainAsync()
    {
        while (true)
        {
            try { await Task.Delay(_saveDelay, _shutdown.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
            (Guid TaskId, GitCommitDraftState State, string Text, long Expected)[] pending;
            lock (_gate)
            {
                pending = _states
                    .Where(pair => pair.Value.NeedsSaving && !pair.Value.Conflicted)
                    .Select(pair => (pair.Key, pair.Value, pair.Value.Text, pair.Value.Revision))
                    .ToArray();
                if (pending.Length == 0)
                {
                    _writer = Task.CompletedTask;
                    return;
                }
            }
            foreach (var item in pending) await SaveOneAsync(item).ConfigureAwait(false);
        }
    }

    private async Task SaveOneAsync((Guid TaskId, GitCommitDraftState State, string Text, long Expected) item)
    {
        bool saved;
        try
        {
            saved = await _repository!.SaveAsync(
                new(item.TaskId, item.Text, item.Expected), _shutdown.Token).ConfigureAwait(false);
        }
        catch (Exception) { saved = false; }
        lock (_gate)
        {
            if (!saved)
            {
                item.State.Conflicted = true;
                return;
            }
            item.State.Persisted = item.Text;
            item.State.Revision = item.Text.Length == 0 ? 0 : item.Expected + 1;
        }
    }

    public async Task FlushAsync()
    {
        while (true)
        {
            Task writer;
            lock (_gate)
            {
                var hasPending = _states.Values.Any(state => state.NeedsSaving && !state.Conflicted);
                if (_writer.IsCompleted)
                {
                    if (!hasPending || _repository is null) return;
                    _writer = Task.Run(DrainAsync);
                }
                writer = _writer;
            }
            await writer.ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
        }
        // Best-effort flush with a bounded wait; afterwards cancel and abandon the rest.
        try { await FlushAsync().WaitAsync(DisposeWait).ConfigureAwait(false); }
        catch (Exception) { }
        await _shutdown.CancelAsync().ConfigureAwait(false);
        Task writer;
        lock (_gate) writer = _writer;
        try { await writer.WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false); }
        catch (Exception) { }
        _shutdown.Dispose();
    }
}
