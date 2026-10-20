using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class PullRequestAnnotationsViewModel : ObservableObject, IDisposable
{
    private readonly PullRequestInspectionPageViewModel<PullRequestFile> _files;
    private readonly PullRequestInspectionPageViewModel<PullRequestComment> _inline;
    private readonly Action _showFiles;
    private PullRequestRevision? _revision;
    private IReadOnlyList<CodePreviewAnnotation> _fileAnnotations = [];
    private CodePreviewAnnotationSelection? _selection;
    private long _generation;
    private long _navigation;
    private bool _disposed;

    public PullRequestAnnotationsViewModel(PullRequestInspectionPageViewModel<PullRequestFile> files,
        PullRequestInspectionPageViewModel<PullRequestComment> inline, Action showFiles)
    {
        _files = files; _inline = inline; _showFiles = showFiles;
        files.Items.CollectionChanged += Changed; inline.Items.CollectionChanged += Changed;
        files.PropertyChanged += FileChanged;
    }

    public ObservableCollection<PullRequestCommentAnchorViewModel> Comments { get; } = [];
    public IReadOnlyList<CodePreviewAnnotation> FileAnnotations { get => _fileAnnotations; private set => SetProperty(ref _fileAnnotations, value); }
    public CodePreviewAnnotationSelection? Selection { get => _selection; private set => SetProperty(ref _selection, value); }

    public void Attach(PullRequestRevision revision) { _revision = revision; Refresh(); }
    public void Clear() { _revision = null; Refresh(); }
    private void Changed(object? sender, NotifyCollectionChangedEventArgs args) => Refresh();
    private void FileChanged(object? sender, PropertyChangedEventArgs args)
    { if (args.PropertyName == nameof(_files.SelectedItem)) Refresh(); }

    private void Refresh()
    {
        var generation = ++_generation;
        Comments.Clear(); Selection = null; FileAnnotations = [];
        if (_disposed || _revision is null) return;
        foreach (var comment in _inline.Items)
        {
            var file = _files.Items.FirstOrDefault(item => item.Path.Equals(comment.Path, StringComparison.Ordinal));
            var state = PullRequestCommentLocator.Locate(_revision, file, comment);
            Comments.Add(new(comment, state, Locate, () => !_disposed && _revision is not null && generation == _generation));
        }
        FileAnnotations = Comments.Where(item => item.State == PullRequestCommentLocationState.Current
                && string.Equals(item.Comment.Path, _files.SelectedItem?.Path, StringComparison.Ordinal))
            .Select(item => new CodePreviewAnnotation(item.Comment.Id, item.Comment.Line!.Value, item.Comment.Side!,
                item.Comment.Author, item.Comment.Body, item.Comment.StartLine)).ToArray();
    }

    private void Locate(PullRequestComment comment)
    {
        if (_disposed || _revision is null || !_inline.Items.Contains(comment)) return;
        var file = _files.Items.FirstOrDefault(item => item.Path.Equals(comment.Path, StringComparison.Ordinal));
        if (PullRequestCommentLocator.Locate(_revision, file, comment) != PullRequestCommentLocationState.Current) return;
        _files.SelectedItem = file;
        _showFiles();
        Selection = new(comment.Id, ++_navigation);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _files.Items.CollectionChanged -= Changed; _inline.Items.CollectionChanged -= Changed;
        _files.PropertyChanged -= FileChanged; Clear();
    }
}
