using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
    private string _status = string.Empty;
    private bool _disposed;

    public WorkspaceAttachmentManagerViewModel(
        IFilePickerService filePicker,
        IWorkspaceAttachmentReader reader)
    {
        _filePicker = filePicker;
        _reader = reader;
        AddFilesCommand = new AsyncRelayCommand<ProjectProfile?>(AddFilesAsync, _ => !_disposed);
        Items.CollectionChanged += HandleItemsChanged;
    }

    public ObservableCollection<WorkspaceAttachmentViewModel> Items { get; } = [];

    public IAsyncRelayCommand<ProjectProfile?> AddFilesCommand { get; }

    public bool HasItems => Items.Count > 0;

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public string? BuildContext() =>
        WorkspaceAttachmentContextBuilder.Build(Items.Select(static item => item.Content));

    public void Consume(IEnumerable<WorkspaceAttachmentViewModel> attachments)
    {
        foreach (var attachment in attachments.ToArray()) Items.Remove(attachment);
    }

    public void Clear()
    {
        Items.Clear();
        Status = string.Empty;
    }

    private async Task AddFilesAsync(ProjectProfile? project)
    {
        if (_disposed) return;
        if (project is null)
        {
            Status = "请先选择项目。";
            return;
        }

        try
        {
            var paths = await _filePicker.PickFilesAsync(CancellationToken.None);
            if (_disposed) return;
            var newPaths = paths
                .Where(path => Items.All(item => !item.Content.FullPath.Equals(
                    Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase)))
                .ToArray();
            if (Items.Count + newPaths.Length > WorkspaceAttachmentLimits.MaximumFiles)
            {
                throw new InvalidOperationException(
                    $"每次最多附加 {WorkspaceAttachmentLimits.MaximumFiles} 个文件。");
            }

            var contents = await _reader.ReadAsync(project, newPaths, CancellationToken.None);
            if (_disposed) return;
            foreach (var content in contents)
            {
                Items.Add(new WorkspaceAttachmentViewModel(content, Remove));
            }
            Status = string.Empty;
        }
        catch (Exception exception)
        {
            if (!_disposed) Status = exception.Message;
        }
    }

    private void Remove(WorkspaceAttachmentViewModel attachment)
    {
        Items.Remove(attachment);
    }

    private void HandleItemsChanged(object? sender, NotifyCollectionChangedEventArgs args) => OnPropertyChanged(nameof(HasItems));

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Items.CollectionChanged -= HandleItemsChanged;
        AddFilesCommand.NotifyCanExecuteChanged();
    }
}
