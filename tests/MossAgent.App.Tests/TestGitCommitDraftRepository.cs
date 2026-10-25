using MossAgent.Application.Persistence;

namespace MossAgent.App.Tests;

internal sealed class TestGitCommitDraftRepository : IGitCommitDraftRepository
{
    private readonly Dictionary<Guid, GitCommitDraft> _saved = [];
    private readonly object _gate = new();

    public Func<Guid, CancellationToken, Task<GitCommitDraft?>>? ReadHandler { get; set; }

    public Task<GitCommitDraft?> GetAsync(Guid taskId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (ReadHandler is { } handler) return handler(taskId, cancellationToken);
        lock (_gate) return Task.FromResult(_saved.GetValueOrDefault(taskId));
    }

    public Task<bool> SaveAsync(GitCommitDraftUpdate update, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            var current = _saved.GetValueOrDefault(update.TaskId)?.Revision ?? 0;
            // Empty text is the delete path: revision 0 clears unconditionally.
            if (update.Text.Length == 0)
            {
                if (update.ExpectedRevision != 0 && current != update.ExpectedRevision)
                    return Task.FromResult(false);
                _saved.Remove(update.TaskId);
                return Task.FromResult(true);
            }
            if (current != update.ExpectedRevision) return Task.FromResult(false);
            _saved[update.TaskId] = new(update.Text, update.ExpectedRevision + 1);
            return Task.FromResult(true);
        }
    }

    public Task<bool> DeleteAsync(Guid taskId, long expectedRevision, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            var current = _saved.GetValueOrDefault(taskId)?.Revision ?? 0;
            if (expectedRevision != 0 && current != expectedRevision) return Task.FromResult(false);
            _saved.Remove(taskId);
            return Task.FromResult(true);
        }
    }

    public void Seed(Guid taskId, string text, long revision)
    {
        lock (_gate) _saved[taskId] = new(text, revision);
    }
}
