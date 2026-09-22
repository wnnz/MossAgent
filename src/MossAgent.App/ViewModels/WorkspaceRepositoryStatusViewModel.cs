using CommunityToolkit.Mvvm.ComponentModel;
using MossAgent.Application.Git;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceRepositoryStatusViewModel(
    IGitRepositoryInfoService gitRepositoryInfo) : ObservableObject
{
    private string _branchLabel = "Git 状态未知";
    private int _refreshVersion;

    public string BranchLabel
    {
        get => _branchLabel;
        private set => SetProperty(ref _branchLabel, value);
    }

    public async Task RefreshAsync(ProjectProfile? project, AgentTask? task)
    {
        var version = Interlocked.Increment(ref _refreshVersion);
        if (project is null)
        {
            BranchLabel = "未选择仓库";
            return;
        }

        var directory = task?.WorktreePath ?? project.PrimaryDirectory;
        var branch = await gitRepositoryInfo.GetBranchNameAsync(
            directory, CancellationToken.None);
        if (version == _refreshVersion)
        {
            BranchLabel = branch is null ? "非 Git 仓库" : branch;
        }
    }
}
