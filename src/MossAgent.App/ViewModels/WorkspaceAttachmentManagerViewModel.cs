using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.Application.Attachments;
using MossAgent.App.Services;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceAttachmentManagerViewModel : ObservableObject
{
    private readonly IFilePickerService _filePicker;
    private readonly IWorkspaceAttachmentReader _reader;
    private string _status = string.Empty;

    public WorkspaceAttachmentManagerViewModel(
        IFilePickerService filePicker,
        IWorkspaceAttachmentReader reader)
    {
        _filePicker = filePicker;
        _reader = reader;
        AddFilesCommand = new AsyncRelayCommand<ProjectProfile?>(AddFilesAsync);
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

    public void Clear()
    {
        Items.Clear();
        Status = string.Empty;
        OnPropertyChanged(nameof(HasItems));
    }

    private async Task AddFilesAsync(ProjectProfile? project)
    {
        if (project is null)
        {
            Status = "请先选择项目。";
            return;
        }

        try
        {
            var paths = await _filePicker.PickFilesAsync(CancellationToken.None);
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
            foreach (var content in contents)
            {
                Items.Add(new WorkspaceAttachmentViewModel(content, Remove));
            }
            Status = string.Empty;
            OnPropertyChanged(nameof(HasItems));
        }
        catch (Exception exception)
        {
            Status = exception.Message;
        }
    }

    private void Remove(WorkspaceAttachmentViewModel attachment)
    {
        Items.Remove(attachment);
        OnPropertyChanged(nameof(HasItems));
    }
}
