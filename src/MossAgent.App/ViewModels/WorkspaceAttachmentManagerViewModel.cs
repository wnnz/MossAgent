using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.Application.Attachments;
using MossAgent.App.Services;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceAttachmentManagerViewModel : ObservableObject, IDisposable
{
    private readonly IFilePickerService _filePicker;
    private readonly IWorkspaceAttachmentReader _reader;
    private readonly ISelectedAttachmentReader? _selectedFiles;
    private readonly IAttachmentThumbnailRenderer? _thumbnailRenderer;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _operationTimeout;
    private CancellationTokenSource? _selection;
    private Guid? _projectId;
    private Guid? _taskId;
    private string? _primaryDirectory;
    private string[] _authorizedDirectories = [];
    private int _contextVersion;
    private string _status = string.Empty;
    private ModelProfile? _model;
    private bool _stopping;
    private bool _disposed;

    public WorkspaceAttachmentManagerViewModel(
        IFilePickerService filePicker,
        IWorkspaceAttachmentReader reader,
        TimeSpan? operationTimeout = null,
        TimeProvider? timeProvider = null,
        ISelectedAttachmentReader? selectedReader = null,
        IAttachmentThumbnailRenderer? thumbnailRenderer = null)
    {
        _filePicker = filePicker;
        _reader = reader;
        _selectedFiles = selectedReader ?? reader as ISelectedAttachmentReader;
        _thumbnailRenderer = thumbnailRenderer;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _operationTimeout = operationTimeout ?? TimeSpan.FromMinutes(5);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_operationTimeout, TimeSpan.Zero);
        AddFilesCommand = new AsyncRelayCommand<ProjectProfile?>(AddFilesAsync, CanAddFiles);
        Items.CollectionChanged += HandleItemsChanged;
    }

    public ObservableCollection<WorkspaceAttachmentViewModel> Items { get; } = [];

    public IAsyncRelayCommand<ProjectProfile?> AddFilesCommand { get; }
    public string AddFilesLabel => WorkspaceText.Get(nameof(AddFilesLabel));
    public string SelectionHint => WorkspaceText.Get("AttachmentSelectionHint");

    public bool HasItems => Items.Count > 0;
    public bool HasImageItems => Items.Any(item => item.Content.Image is not null);
    public bool IsModelCompatible => !HasImageItems || _model?.SupportsImages == true;

    public string Status
    {
        get => IsModelCompatible ? _status : WorkspaceText.Get("AttachmentImageModelUnsupported");
        private set => SetProperty(ref _status, value);
    }

    public void SetModel(ModelProfile? model)
    {
        _model = model;
        OnPropertyChanged(nameof(IsModelCompatible));
        OnPropertyChanged(nameof(Status));
    }

    public string? BuildContext() =>
        WorkspaceAttachmentContextBuilder.Build(Items.Select(static item => item.Content));

    public void Consume(IEnumerable<WorkspaceAttachmentViewModel> attachments)
    {
        foreach (var attachment in attachments.ToArray()) Items.Remove(attachment);
    }

    public void Clear()
    {
        InvalidateSelection();
        Items.Clear();
        Status = string.Empty;
    }

    public void SetContext(ProjectProfile? project, Guid? taskId, bool preserveItems = false)
    {
        if (_disposed || _stopping) return;
        var sameProject = MatchesProject(project);
        if (sameProject && taskId == _taskId) return;
        _projectId = project?.Id;
        _primaryDirectory = project?.PrimaryDirectory;
        _authorizedDirectories = project?.AuthorizedDirectories.ToArray() ?? [];
        _taskId = taskId;
        InvalidateSelection();
        if (!preserveItems || !sameProject) Items.Clear();
        Status = string.Empty;
        AddFilesCommand.NotifyCanExecuteChanged();
    }

    private bool MatchesProject(ProjectProfile? project) => project?.Id == _projectId
        && string.Equals(project?.PrimaryDirectory, _primaryDirectory, StringComparison.OrdinalIgnoreCase)
        && (project?.AuthorizedDirectories ?? []).SequenceEqual(_authorizedDirectories, StringComparer.OrdinalIgnoreCase);

    private bool CanAddFiles(ProjectProfile? project) => !_disposed && !_stopping && _selection is null
        && (project is not null || _selectedFiles is not null) && MatchesProject(project) && Items.Count < WorkspaceAttachmentLimits.MaximumFiles;

    private async Task AddFilesAsync(ProjectProfile? project, CancellationToken commandToken)
    {
        if (_disposed || _stopping || _selection is not null) return;
        if (!MatchesProject(project) || project is null && _selectedFiles is null)
        {
            Status = WorkspaceText.Get(project is null ? "AttachmentProjectRequired" : "AttachmentContextChanged");
            return;
        }

        var snapshot = project is null ? null : project with { AuthorizedDirectories = _authorizedDirectories.ToArray() };
        var version = _contextVersion;
        using var timeout = new CancellationTokenSource(_operationTimeout, _timeProvider);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(commandToken, timeout.Token);
        _selection = cancellation;
        bool IsOwnerCurrent() => !_disposed && !_stopping && version == _contextVersion;
        bool IsCurrent() => IsOwnerCurrent() && !cancellation.IsCancellationRequested && MatchesProject(project);
        Status = string.Empty;
        AddFilesCommand.NotifyCanExecuteChanged();
        try
        {
            var paths = await _filePicker.PickFilesAsync(cancellation.Token).WaitAsync(cancellation.Token);
            if (!IsCurrent() || paths.Count == 0) return;
            var newPaths = SelectNewPaths(paths);
            if (newPaths is null || newPaths.Length == 0) return;
            var reading = snapshot is null ? _selectedFiles!.ReadSelectedAsync(newPaths, cancellation.Token)
                : _reader.ReadAsync(snapshot, newPaths, cancellation.Token);
            var contents = await reading.WaitAsync(cancellation.Token);
            if (!IsCurrent()) return;
            if (snapshot is null && (contents.Count != newPaths.Length
                || contents.Any(item => !Path.IsPathFullyQualified(item.FullPath))
                || !newPaths.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(contents.Select(item => item.FullPath))))
                throw new UnauthorizedAccessException("Attachment results do not match the explicit selection.");
            if (Items.Count + contents.Count > WorkspaceAttachmentLimits.MaximumFiles)
            { ShowFileLimit(); return; }
            if (Items.Select(item => item.Content).Concat(contents).Where(item => item.Image is not null)
                .Sum(item => item.ByteCount) > AttachmentImage.MaximumTotalByteCount)
            { Status = WorkspaceText.Get("AttachmentImageSizeLimit"); return; }
            if (Items.Select(item => item.Content).Concat(contents).Where(item => item.Document is not null)
                .Sum(item => item.ByteCount) > AttachmentDocument.MaximumTotalByteCount)
            { Status = WorkspaceText.Get("AttachmentDocumentSizeLimit"); return; }
            foreach (var content in contents)
            {
                Items.Add(CreateItem(content,
                    authorizationStamp: snapshot is null ? WorkspaceAttachmentAuthorization.CreateSelection(content.FullPath)
                        : WorkspaceAttachmentAuthorization.Create(snapshot)));
            }
            Status = string.Empty;
        }
        catch (OperationCanceledException)
        {
            if (IsOwnerCurrent()) Status = WorkspaceText.Get(timeout.IsCancellationRequested
                ? "AttachmentTimedOut" : "AttachmentCancelled");
        }
        catch (Exception exception)
        {
            if (IsCurrent()) Status = WorkspaceText.Get(exception switch
            {
                AttachmentImageValidationException image => "AttachmentImage" + image.Failure,
                AttachmentDocumentValidationException document => "AttachmentDocument" + document.Failure,
                UnauthorizedAccessException => "AttachmentUnauthorized",
                FileNotFoundException or DirectoryNotFoundException => "AttachmentMissing",
                _ => "AttachmentFailed"
            });
        }
        finally
        {
            if (ReferenceEquals(_selection, cancellation)) _selection = null;
            if (!_disposed) AddFilesCommand.NotifyCanExecuteChanged();
        }
    }

    private string[]? SelectNewPaths(IReadOnlyList<string> paths)
    {
        if (_projectId is null && paths.Any(path => !Path.IsPathFullyQualified(path)))
            throw new UnauthorizedAccessException("Explicit file selections require absolute paths.");
        if (paths.Count > WorkspaceAttachmentLimits.MaximumFiles)
        { ShowFileLimit(); return null; }
        var selected = paths.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(path => Items.All(item => !item.Content.FullPath.Equals(path, StringComparison.OrdinalIgnoreCase))).ToArray();
        if (Items.Count + selected.Length <= WorkspaceAttachmentLimits.MaximumFiles) return selected;
        ShowFileLimit();
        return null;
    }

    private void ShowFileLimit() => Status = string.Format(CultureInfo.CurrentCulture,
        WorkspaceText.Get("AttachmentFileLimit"), WorkspaceAttachmentLimits.MaximumFiles);

    private void InvalidateSelection()
    {
        _contextVersion++;
        _selection?.Cancel();
    }

    public void Stop()
    {
        if (_stopping) return;
        _stopping = true;
        InvalidateSelection();
        AddFilesCommand.NotifyCanExecuteChanged();
    }

    private void Remove(WorkspaceAttachmentViewModel attachment)
    {
        Items.Remove(attachment);
    }

    internal WorkspaceAttachmentViewModel CreateItem(WorkspaceAttachmentContent content,
        Guid? selectionId = null, string? authorizationStamp = null, bool isAvailable = true) =>
        new(content, Remove, selectionId, authorizationStamp, isAvailable, _thumbnailRenderer);

    private void HandleItemsChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(HasImageItems));
        OnPropertyChanged(nameof(IsModelCompatible));
        OnPropertyChanged(nameof(Status));
        AddFilesCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_disposed) return;
        Stop();
        _disposed = true;
        Items.CollectionChanged -= HandleItemsChanged;
        AddFilesCommand.NotifyCanExecuteChanged();
    }
}
