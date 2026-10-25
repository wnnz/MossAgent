using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Threading;
using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceComposerDraftsViewModel : ObservableObject, IDisposable
{
    private readonly IConversationDraftRepository? _repository;
    private readonly SettingsOperationLifetime _operations;
    private readonly Dictionary<ConversationDraftScope, WorkspaceComposerDraftEntry> _entries = [];
    private readonly object _gate = new();
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly WorkspaceSteeringDraftCoordinator _steering;
    private readonly TimeSpan _saveDelay;
    private readonly SynchronizationContext? _notificationContext = SynchronizationContext.Current is AvaloniaSynchronizationContext
        ? SynchronizationContext.Current : null;
    private WorkspaceComposerDraftEntry _current;
    private Task _writeTask = Task.CompletedTask;
    private Task _recoveryTask = Task.CompletedTask;
    private Task? _stopTask;
    private bool _stopping;
    private bool _disposed;
    private bool _recovering;

    public WorkspaceComposerDraftsViewModel(IConversationDraftRepository? repository = null,
        TimeSpan? operationTimeout = null, TimeSpan? saveDelay = null)
    {
        _repository = repository;
        _operations = new(operationTimeout);
        _saveDelay = saveDelay ?? TimeSpan.FromMilliseconds(200);
        _current = GetEntry(new(null, null));
        _steering = new(_gate, _writeGate, GetEntry, () => _disposed || _stopping || _recovering, () => { Publish(); StartWrites(); });
        RetryCommand = new AsyncRelayCommand(RetryAsync, () => !_disposed && !_recovering);
        KeepLocalCommand = new AsyncRelayCommand(KeepLocalAsync, () => HasConflicts && !_disposed && !_recovering);
        RemoveUnavailableAttachmentsCommand = new RelayCommand(RemoveUnavailableAttachments,
            () => HasUnavailableAttachments && !_disposed && !_recovering);
    }

    public ConversationDraftScope Scope => _current.Scope;
    public string Text
    {
        get => _current.Text;
        set
        {
            value ??= string.Empty;
            WorkspaceComposerDraftEntry entry;
            lock (_gate)
            {
                if (_disposed || _current.Text == value) return;
                entry = _current;
                entry.Text = value;
                entry.Edited = true;
            }
            Publish();
            _ = LoadEntryAsync(entry);
            StartWrites();
        }
    }

    public IReadOnlyList<ConversationDraftAttachment> Attachments => _current.Attachments;
    public bool CanSend => !_disposed && !_stopping && !_recovering && _current.Loaded && _current.ErrorKey is null
        && _current.CanSave && _current.AttachmentsAuthorized;
    public bool IsConflict => _current.ErrorKey == "DraftConflict";
    public bool HasConflicts => Entries().Any(entry => entry.ErrorKey == "DraftConflict");
    public bool HasUnavailableAttachments => Entries().Any(entry => entry.Attachments.Any(attachment => !attachment.IsAvailable));
    public bool HasNotice => !_current.Loaded || Text.Length > ConversationDraft.MaximumTextLength
        || !_current.AttachmentsAuthorized || Attachments.Any(attachment => !attachment.IsAvailable)
        || Entries().Any(entry => entry.ErrorKey is not null || entry.NeedsSaving && !entry.CanSave);
    public string Notice => WorkspaceText.Get(Text.Length > ConversationDraft.MaximumTextLength ? "DraftTooLong"
        : Attachments.Any(attachment => !attachment.IsAvailable) ? "DraftAttachmentUnavailable"
        : !_current.AttachmentsAuthorized ? "DraftAttachmentUnauthorized"
        : _current.ErrorKey ?? (!_current.Loaded ? "DraftLoading" : "DraftOtherSaveFailed"));
    public string RetryLabel => WorkspaceText.Get("DraftRetry");
    public string KeepLocalLabel => WorkspaceText.Get("DraftKeepLocal");
    public string RemoveUnavailableAttachmentsLabel => WorkspaceText.Get("DraftRemoveUnavailableAttachments");
    public IAsyncRelayCommand RetryCommand { get; }
    public IAsyncRelayCommand KeepLocalCommand { get; }
    public IRelayCommand RemoveUnavailableAttachmentsCommand { get; }

    public bool SelectContext(Guid? projectId, Guid? taskId)
    {
        WorkspaceComposerDraftEntry entry;
        bool changed;
        lock (_gate)
        {
            if (_disposed || _stopping) return false;
            var scope = new ConversationDraftScope(projectId, taskId);
            changed = Scope != scope;
            entry = _current = GetEntry(scope);
        }
        _ = LoadEntryAsync(entry);
        Publish();
        return changed;
    }

    public Task LoadCurrentAsync() => LoadEntryAsync(_current);

    public async Task<bool> TrySeedAsync(ConversationDraftScope scope, string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > ConversationDraft.MaximumTextLength) return false;
        WorkspaceComposerDraftEntry entry;
        lock (_gate) { if (_disposed || _stopping) return false; entry = GetEntry(scope); }
        await LoadEntryAsync(entry).WaitAsync(cancellationToken);
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_disposed || _stopping || !entry.Loaded || entry.ErrorKey is not null || entry.NeedsSaving
                || entry.Text.Length != 0 || entry.Attachments.Length != 0 || !entry.CanSave) return false;
            entry.Text = text; entry.Edited = true;
        }
        Publish(); StartWrites(); await WaitForWritesAsync().WaitAsync(cancellationToken);
        lock (_gate) return entry.ErrorKey is null && entry.Text == text && !entry.NeedsSaving;
    }

    public void SetAttachments(ConversationDraftScope scope, IReadOnlyList<ConversationDraftAttachment> attachments)
    {
        WorkspaceComposerDraftEntry entry;
        lock (_gate)
        {
            if (_disposed || _current.Scope != scope || _current.Attachments.SequenceEqual(attachments)) return;
            entry = _current;
            entry.Attachments = attachments.ToArray();
            entry.AttachmentsEdited = true;
        }
        Publish();
        _ = LoadEntryAsync(entry);
        StartWrites();
    }

    public void SetAttachmentAuthorization(ConversationDraftScope scope, bool authorized)
    {
        lock (_gate)
        {
            if (_disposed || _current.Scope != scope || _current.AttachmentsAuthorized == authorized) return;
            _current.AttachmentsAuthorized = authorized;
        }
        Publish();
    }

    private void RemoveUnavailableAttachments()
    {
        lock (_gate)
        {
            if (_disposed || _recovering) return;
            foreach (var entry in _entries.Values.Where(entry => entry.Attachments.Any(attachment => !attachment.IsAvailable)))
            {
                entry.Attachments = entry.Attachments.Where(attachment => attachment.IsAvailable).ToArray();
                entry.AttachmentsEdited = true;
                if (entry.Attachments.Length == 0) entry.AttachmentsAuthorized = true;
            }
        }
        Publish();
        StartWrites();
    }

    public void AcceptSubmission(ConversationDraftScope scope, Guid taskId, string sentText,
        IReadOnlyCollection<Guid>? capturedAttachmentIds = null, IReadOnlyList<ConversationDraftUpdate>? committed = null)
    {
        lock (_gate)
        {
            if (_disposed || !_entries.TryGetValue(scope, out var source)) return;
            var target = GetEntry(new(scope.ProjectId, taskId));
            WorkspaceDraftSubmissionState.Accept(source, target, sentText, capturedAttachmentIds, committed);
            if (ReferenceEquals(_current, source)) _current = target;
        }
        Publish();
        StartWrites();
    }

    public WorkspaceDraftSubmission? CreateSubmission(IConversationSubmissionRepository? repository,
        ConversationDraftScope scope, string sentText, IReadOnlyCollection<Guid> attachmentIds) =>
        repository is null || _repository is null || !repository.SupportsDrafts(_repository) ? null : new(repository, _writeGate,
            taskId => CaptureSubmission(scope, taskId, sentText, attachmentIds),
            (taskId, updates) => AcceptSubmission(scope, taskId, sentText, attachmentIds, updates),
            () => { lock (_gate) GetEntry(scope).ErrorKey = "DraftConflict"; Publish(); });

    private ConversationDraftUpdate[] CaptureSubmission(ConversationDraftScope scope, Guid taskId, string sentText,
        IReadOnlyCollection<Guid> attachmentIds)
    {
        lock (_gate)
        {
            if (_disposed || _recovering) throw new InvalidOperationException("The draft is unavailable.");
            return WorkspaceDraftSubmissionState.Capture(GetEntry(scope), GetEntry(new(scope.ProjectId, taskId)), sentText, attachmentIds);
        }
    }

    internal bool CanQueueTo(IConversationFollowupRepository repository) => _repository is not null && repository.SupportsDrafts(_repository);
    internal bool CanSteerTo(MossAgent.Application.Conversations.ConversationSteeringSession session) =>
        _repository is not null && session.SupportsDrafts(_repository);

    internal WorkspaceSteeredDraftSubmission CreateSteeringSubmission(MossAgent.Application.Conversations.ConversationSteeringSession session,
        ConversationDraftScope scope, string sentText) => _repository is null || !session.SupportsDrafts(_repository)
        ? throw new InvalidOperationException("Live input and composer must share the same draft store.") : new(_repository, scope, sentText,
            token => _steering.AcquireAsync(scope, sentText, token),
            (taskId, submitted, updates) => AcceptSubmission(scope, taskId, sentText, submitted, updates),
            () => { lock (_gate) GetEntry(scope).ErrorKey = "DraftConflict"; Publish(); });

    internal WorkspaceQueuedDraftSubmission CreateQueuedSubmission(IConversationFollowupRepository repository,
        ConversationDraftScope scope, string sentText, IReadOnlyCollection<Guid> attachmentIds) => !CanQueueTo(repository)
        ? throw new InvalidOperationException("The queue and composer must share the same draft store.") : new(repository, _writeGate,
            taskId => CaptureSubmission(scope, taskId, sentText, attachmentIds),
            (taskId, updates) => AcceptSubmission(scope, taskId, sentText, attachmentIds, updates),
            () => { lock (_gate) GetEntry(scope).ErrorKey = "DraftConflict"; Publish(); });

    public async Task FlushAsync()
    {
        if (_disposed) return;
        if (_recovering) await _recoveryTask.ConfigureAwait(false);
        await Task.WhenAll(Entries().Select(LoadEntryAsync)).ConfigureAwait(false);
        StartWrites();
        await WaitForWritesAsync().ConfigureAwait(false);
        if (Entries().Any(entry => entry.NeedsSaving))
            throw new InvalidOperationException("Conversation drafts could not be saved.");
    }

    public Task StopAsync()
    {
        Task stopping;
        lock (_gate)
        {
            if (_disposed) return Task.CompletedTask;
            if (_stopTask?.IsCompleted == false) return _stopTask;
            _stopping = true;
            stopping = _stopTask = StopCoreAsync();
        }
        Publish();
        return stopping;
    }

    private async Task StopCoreAsync()
    {
        await RetryAsync().ConfigureAwait(false);
        await FlushAsync().ConfigureAwait(false);
    }

    private WorkspaceComposerDraftEntry GetEntry(ConversationDraftScope scope)
    {
        lock (_gate)
        {
            if (_entries.TryGetValue(scope, out var entry)) return entry;
            entry = new(scope) { Loaded = _repository is null };
            _entries.Add(scope, entry);
            return entry;
        }
    }

    private WorkspaceComposerDraftEntry[] Entries()
    {
        lock (_gate) return _entries.Values.ToArray();
    }

    private CancellationTokenSource? CreateOperation()
    {
        lock (_gate) return _disposed ? null : _operations.CreateOperation(_operations.Token);
    }

    private Task LoadEntryAsync(WorkspaceComposerDraftEntry entry)
    {
        lock (_gate)
        {
            if (_disposed) return Task.CompletedTask;
            if (entry.ReadTask is { IsCompleted: false } reading) return reading;
            if (entry.Loaded || entry.ErrorKey is not null) return Task.CompletedTask;
            return entry.ReadTask ??= Task.Run(() => ReadCoreAsync(entry));
        }
    }

    private async Task ReadCoreAsync(WorkspaceComposerDraftEntry entry)
    {
        using var operation = CreateOperation();
        if (operation is null) return;
        try
        {
            var saved = await _repository!.GetAsync(entry.Scope, operation.Token).WaitAsync(operation.Token);
            lock (_gate)
            {
                if (_disposed) return;
                entry.SavedText = saved?.Text ?? string.Empty;
                entry.SavedAttachments = saved?.AttachmentItems.ToArray() ?? [];
                entry.Revision = saved?.Revision ?? 0;
                if (!entry.Edited) entry.Text = entry.SavedText;
                if (!entry.AttachmentsEdited) entry.Attachments = entry.SavedAttachments;
                entry.Loaded = true;
            }
            StartWrites();
        }
        catch (Exception)
        {
            lock (_gate) { if (!_disposed) entry.ErrorKey = "DraftLoadFailed"; }
        }
        if (!_disposed) Publish();
    }

    private void StartWrites(bool duringRecovery = false)
    {
        lock (_gate)
        {
            if (!_disposed && (!_recovering || duringRecovery) && _writeTask.IsCompleted) _writeTask = Task.Run(DrainWritesAsync);
        }
    }

    private async Task WaitForWritesAsync()
    {
        while (true)
        {
            Task writing;
            lock (_gate) writing = _writeTask;
            await writing.ConfigureAwait(false);
            lock (_gate) { if (_writeTask.IsCompleted) return; }
        }
    }

    private async Task DrainWritesAsync()
    {
        while (!_disposed)
        {
            try { await Task.Delay(_saveDelay, _operations.Token); }
            catch (OperationCanceledException) { return; }
            using var lease = await WorkspaceDraftWriteLease.AcquireAsync(_writeGate, _operations.Token);
            WorkspaceComposerDraftEntry[] pending;
            ConversationDraftUpdate[] updates;
            lock (_gate)
            {
                if (_disposed) return;
                pending = _entries.Values.Where(entry => entry.Loaded && entry.NeedsSaving && !entry.IsSteeringSubmission
                    && entry.ErrorKey is null && entry.CanSave).ToArray();
                if (pending.Length == 0)
                {
                    _writeTask = Task.CompletedTask;
                    return;
                }
                updates = pending.Select(entry => new ConversationDraftUpdate(entry.Scope, entry.Text, entry.Revision,
                    entry.Attachments.Length == 0 ? null : entry.Attachments.ToArray())).ToArray();
                for (var index = 0; index < pending.Length; index++) pending[index].Attempt = updates[index];
            }
            using var operation = CreateOperation();
            if (operation is null) return;
            try
            {
                var saved = _repository is null || await _repository.SaveAsync(updates, operation.Token).WaitAsync(operation.Token);
                lock (_gate)
                {
                    if (_disposed) return;
                    for (var index = 0; index < pending.Length; index++)
                    {
                        var entry = pending[index];
                        if (!saved) entry.ErrorKey = "DraftConflict";
                        else
                        {
                            entry.SavedText = updates[index].Text;
                            entry.SavedAttachments = updates[index].AttachmentItems.ToArray();
                            entry.Revision = updates[index].ExpectedRevision + 1;
                        }
                    }
                }
            }
            catch (Exception)
            {
                lock (_gate)
                {
                    if (_disposed) return;
                    foreach (var entry in pending) entry.ErrorKey = "DraftSaveFailed";
                }
            }
            Publish();
        }
    }

    private Task RetryAsync() => RecoverAsync(overwrite: false);
    private Task KeepLocalAsync() => RecoverAsync(overwrite: true);

    private Task RecoverAsync(bool overwrite)
    {
        lock (_gate)
        {
            if (_disposed) return Task.CompletedTask;
            if (_recovering) return _recoveryTask;
            _recovering = true;
            return _recoveryTask = Task.Run(() => RecoverCoreAsync(overwrite));
        }
    }

    private async Task RecoverCoreAsync(bool overwrite)
    {
        Publish();
        try
        {
            await WaitForWritesAsync();
            if (_disposed) return;
            foreach (var entry in Entries().Where(entry => entry.ErrorKey is not null))
            {
                if (_disposed) return;
                if (!entry.Loaded)
                {
                    lock (_gate) { entry.ErrorKey = null; entry.ReadTask = null; }
                    await LoadEntryAsync(entry);
                }
                else await ReconcileAsync(entry, overwrite && entry.ErrorKey == "DraftConflict");
            }
            StartWrites(duringRecovery: true);
            await WaitForWritesAsync();
        }
        finally
        {
            lock (_gate) _recovering = false;
            Publish();
        }
    }

    private async Task ReconcileAsync(WorkspaceComposerDraftEntry entry, bool overwrite)
    {
        using var operation = CreateOperation();
        if (operation is null) return;
        try
        {
            var saved = _repository is null ? null
                : await _repository.GetAsync(entry.Scope, operation.Token).WaitAsync(operation.Token);
            lock (_gate)
            {
                if (_disposed) return;
                var text = saved?.Text ?? string.Empty;
                var revision = saved?.Revision ?? 0;
                var attachments = saved?.AttachmentItems.ToArray() ?? [];
                if (overwrite || (revision == entry.Revision && text == entry.SavedText && attachments.SequenceEqual(entry.SavedAttachments))
                    || (entry.Attempt is { } attempt && revision == attempt.ExpectedRevision + 1 && text == attempt.Text
                        && attachments.SequenceEqual(attempt.AttachmentItems)))
                {
                    entry.Revision = revision;
                    entry.SavedText = text;
                    entry.SavedAttachments = attachments;
                    entry.ErrorKey = null;
                }
                else entry.ErrorKey = "DraftConflict";
            }
        }
        catch (Exception)
        {
            lock (_gate) { if (!_disposed) entry.ErrorKey = "DraftSaveFailed"; }
        }
    }

    private void Publish()
    {
        lock (_gate) { if (_disposed) return; }
        if (_notificationContext is not null && !ReferenceEquals(SynchronizationContext.Current, _notificationContext))
        {
            _notificationContext.Post(static state => ((WorkspaceComposerDraftsViewModel)state!).PublishCore(), this);
            return;
        }
        PublishCore();
    }

    private void PublishCore()
    {
        lock (_gate) { if (_disposed) return; }
        OnPropertyChanged(nameof(Attachments));
        OnPropertyChanged(nameof(Text));
        OnPropertyChanged(nameof(CanSend));
        OnPropertyChanged(nameof(HasNotice));
        OnPropertyChanged(nameof(Notice));
        OnPropertyChanged(nameof(IsConflict));
        OnPropertyChanged(nameof(HasConflicts));
        OnPropertyChanged(nameof(HasUnavailableAttachments));
        RetryCommand.NotifyCanExecuteChanged();
        KeepLocalCommand.NotifyCanExecuteChanged();
        RemoveUnavailableAttachmentsCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
        }
        _operations.Dispose();
        RetryCommand.NotifyCanExecuteChanged();
        KeepLocalCommand.NotifyCanExecuteChanged();
        RemoveUnavailableAttachmentsCommand.NotifyCanExecuteChanged();
    }
}
