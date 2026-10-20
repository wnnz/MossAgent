using System.Collections.Specialized;
using System.ComponentModel;
using MossAgent.Application.Attachments;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

internal sealed class WorkspaceAttachmentDraftCoordinator : IDisposable
{
    private readonly WorkspaceAttachmentManagerViewModel _attachments;
    private readonly WorkspaceComposerDraftsViewModel _drafts;
    private readonly object _gate = new();
    private ConversationDraftScope _scope = new(null, null);
    private ProjectProfile? _project;
    private IReadOnlyList<ConversationDraftAttachment>? _lastApplied;
    private bool _synchronizing;
    private bool _disposed;

    public WorkspaceAttachmentDraftCoordinator(WorkspaceAttachmentManagerViewModel attachments,
        WorkspaceComposerDraftsViewModel drafts)
    {
        _attachments = attachments;
        _drafts = drafts;
        attachments.Items.CollectionChanged += HandleItemsChanged;
        drafts.PropertyChanged += HandleDraftChanged;
    }

    public bool SelectContext(ProjectProfile? project, Guid? taskId, bool preserveItems = false)
    {
        lock (_gate)
        {
            if (_disposed) return false;
            _synchronizing = true;
            try
            {
                _project = project is null ? null : project with { AuthorizedDirectories = project.AuthorizedDirectories.ToArray() };
                _scope = new(project?.Id, taskId);
                _attachments.SetContext(project, taskId, preserveItems);
                var changed = _drafts.SelectContext(project?.Id, taskId);
                Restore();
                return changed;
            }
            finally { _synchronizing = false; }
        }
    }

    private void HandleItemsChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        lock (_gate)
        {
            if (_disposed || _synchronizing || _scope != _drafts.Scope) return;
            _synchronizing = true;
            try
            {
                var items = _attachments.Items.Select(item => new ConversationDraftAttachment(item.SelectionId,
                    item.FullPath, item.DisplayPath, item.IsAvailable && item.Content.Image is null ? item.Content.Content : null,
                    item.Content.ByteCount, item.AuthorizationStamp, item.Content.MediaType, item.IsAvailable ? item.Content.Image : null)).ToArray();
                _drafts.SetAttachments(_scope, items);
                _lastApplied = _drafts.Attachments;
                UpdateAuthorization(items);
            }
            finally { _synchronizing = false; }
        }
    }

    private void HandleDraftChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(WorkspaceComposerDraftsViewModel.Attachments)) return;
        lock (_gate)
        {
            if (_disposed || _synchronizing || _scope != _drafts.Scope || ReferenceEquals(_lastApplied, _drafts.Attachments)) return;
            _synchronizing = true;
            try { Restore(); }
            finally { _synchronizing = false; }
        }
    }

    private void Restore()
    {
        var items = _drafts.Attachments.ToArray();
        _lastApplied = _drafts.Attachments;
        var existing = _attachments.Items.ToArray();
        var restored = items.Select(item => existing.FirstOrDefault(candidate => Matches(candidate, item))
            ?? _attachments.CreateItem(new WorkspaceAttachmentContent(item.FullPath, item.DisplayPath,
                item.Content ?? string.Empty, item.ByteCount, item.Image) { MediaType = item.MediaType },
                item.Id, item.AuthorizationStamp, item.IsAvailable)).ToArray();
        if (!existing.SequenceEqual(restored))
        {
            _attachments.Items.Clear();
            foreach (var item in restored) _attachments.Items.Add(item);
        }
        UpdateAuthorization(items);
    }

    private static bool Matches(WorkspaceAttachmentViewModel candidate, ConversationDraftAttachment item) =>
        candidate.SelectionId == item.Id && candidate.FullPath == item.FullPath && candidate.DisplayPath == item.DisplayPath
        && candidate.AuthorizationStamp == item.AuthorizationStamp && candidate.Content.ByteCount == item.ByteCount
        && candidate.Content.MediaType == item.MediaType && candidate.IsAvailable == item.IsAvailable
        && (!candidate.IsAvailable || (item.Image is null ? candidate.Content.Content == item.Content
            : candidate.Content.Image?.Base64Data == item.Image.Base64Data));

    private void UpdateAuthorization(IReadOnlyList<ConversationDraftAttachment> items)
    {
        var stamp = _project is null ? null : WorkspaceAttachmentAuthorization.Create(_project);
        _drafts.SetAttachmentAuthorization(_scope, items.All(item => item.AuthorizationStamp
            == (stamp ?? WorkspaceAttachmentAuthorization.CreateSelection(item.FullPath))));
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _attachments.Items.CollectionChanged -= HandleItemsChanged;
            _drafts.PropertyChanged -= HandleDraftChanged;
        }
    }
}
