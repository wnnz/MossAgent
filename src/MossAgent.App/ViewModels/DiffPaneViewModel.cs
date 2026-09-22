using System.Text.Json;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.Tools.Abstractions;

namespace MossAgent.App.ViewModels;

public sealed class DiffPaneViewModel : ObservableObject
{
    private readonly IToolExecutor _tools;
    private ToolExecutionContext? _context;
    private string _content = string.Empty;
    private string _status = "请选择任务后刷新变更。";
    private IReadOnlyList<GitChangedFileViewModel> _allFiles = [];
    private GitChangedFileViewModel? _selectedFile;
    private bool _isStaged;

    public DiffPaneViewModel(IToolExecutor tools)
    {
        _tools = tools;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => _context is not null);
    }

    public IAsyncRelayCommand RefreshCommand { get; }
    public ObservableCollection<GitChangedFileViewModel> Files { get; } = [];
    public string Content { get => _content; private set => SetProperty(ref _content, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public GitChangedFileViewModel? SelectedFile
    {
        get => _selectedFile;
        set
        {
            if (SetProperty(ref _selectedFile, value))
            {
                _ = RefreshSelectedDiffAsync();
            }
        }
    }

    public bool IsStaged
    {
        get => _isStaged;
        set
        {
            if (SetProperty(ref _isStaged, value))
            {
                ApplyFilter();
            }
        }
    }

    public void Attach(ToolExecutionContext context)
    {
        _context = context;
        Content = string.Empty;
        _allFiles = [];
        Files.Clear();
        SelectedFile = null;
        Status = "点击刷新读取 Git 变更。";
        RefreshCommand.NotifyCanExecuteChanged();
    }

    public void Show(string content) => Content = content;

    private async Task RefreshAsync()
    {
        if (_context is null)
        {
            return;
        }

        var statusArguments = JsonSerializer.SerializeToElement(new { nullTerminated = true });
        var request = new ToolRequest(
            Guid.NewGuid().ToString("N"), "git.status", statusArguments);
        var result = await _tools.ExecuteAsync(request, _context, CancellationToken.None);
        if (!result.IsSuccess)
        {
            _allFiles = [];
            Files.Clear();
            Content = result.Summary;
            Status = "读取 Git 状态失败。";
            return;
        }

        _allFiles = GitStatusParser.Parse(result.Content ?? string.Empty);
        ApplyFilter();
        Status = _allFiles.Count == 0
            ? "工作区没有变更。"
            : $"共 {_allFiles.Count} 个变更文件，当前显示 {Files.Count} 个。";
    }

    private void ApplyFilter()
    {
        var filtered = _allFiles.Where(file =>
            IsStaged ? file.HasStagedChange : file.HasUnstagedChange);
        Files.ReplaceWith(filtered);
        SelectedFile = Files.FirstOrDefault();
        if (SelectedFile is null)
        {
            Content = IsStaged ? "没有已暂存变更。" : "没有未暂存变更。";
        }
    }

    private async Task RefreshSelectedDiffAsync()
    {
        if (_context is null || SelectedFile is not { } file)
        {
            return;
        }

        if (!IsStaged && file.Status == "??")
        {
            Content = "这是未跟踪文件；加入暂存区后可显示标准 Git Diff。";
            return;
        }

        var arguments = JsonSerializer.SerializeToElement(new
        {
            staged = IsStaged,
            path = file.Path
        });
        var request = new ToolRequest(
            Guid.NewGuid().ToString("N"), "git.diff", arguments);
        var result = await _tools.ExecuteAsync(request, _context, CancellationToken.None);
        Content = result.Content ?? result.Summary;
    }
}
