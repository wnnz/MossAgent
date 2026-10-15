using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

/// <summary>
/// 项目侧栏导航测试，覆盖跨项目会话展示、搜索和选中态恢复。
/// </summary>
public sealed class WorkspaceProjectNavigationViewModelTests
{
    [Fact]
    public void ToggleExpandedCommand_TogglesProjectTaskVisibility()
    {
        var navigation = new WorkspaceProjectNavigationViewModel(CreateProject("MossAgent"));

        Assert.False(navigation.IsExpanded);
        navigation.ToggleExpandedCommand.Execute(null);
        Assert.True(navigation.IsExpanded);
    }

    /// <summary>
    /// 验证任务筛选不会丢失当前会话高亮，清除搜索后能够恢复完整列表。
    /// </summary>
    [Fact]
    public void ApplyFilter_PreservesSelectionAndRestoresTasks()
    {
        var project = CreateProject("MossAgent");
        var selected = CreateTask(project.Id, "还原 Codex 界面");
        var other = CreateTask(project.Id, "修复终端");
        var navigation = new WorkspaceProjectNavigationViewModel(project);
        navigation.SetTasks([selected, other]);
        navigation.SetSelectedTask(selected.Id);

        navigation.ApplyFilter("Codex");

        Assert.Single(navigation.Tasks);
        Assert.Equal("2 个任务", navigation.ProjectTaskSummary);
        Assert.True(navigation.Tasks[0].IsSelected);
        Assert.True(navigation.IsVisible);

        navigation.ApplyFilter(string.Empty);

        Assert.Equal(2, navigation.Tasks.Count);
        Assert.True(navigation.Tasks.Single(item => item.Task.Id == selected.Id).IsSelected);
    }

    /// <summary>
    /// 验证不匹配项目名称或任务标题的分组会从搜索结果隐藏。
    /// </summary>
    [Fact]
    public void ApplyFilter_HidesUnmatchedProject()
    {
        var project = CreateProject("MossAgent");
        var navigation = new WorkspaceProjectNavigationViewModel(project);
        navigation.SetTasks([CreateTask(project.Id, "修复终端")]);

        navigation.ApplyFilter("浏览器");

        Assert.False(navigation.IsVisible);
        Assert.Empty(navigation.Tasks);
    }

    [Fact]
    public void UpdateProject_ReevaluatesSearchWithoutLosingTasks()
    {
        var project = CreateProject("Original");
        var navigation = new WorkspaceProjectNavigationViewModel(project);
        navigation.SetTasks([CreateTask(project.Id, "修复终端")]);
        navigation.ApplyFilter("Renamed");
        Assert.False(navigation.IsVisible);

        navigation.UpdateProject(project with { Name = "Renamed" });

        Assert.True(navigation.IsVisible);
        Assert.Equal("Renamed", navigation.Project.Name);
        Assert.Single(navigation.Tasks);
    }

    [Fact]
    public void PinnedTaskIsNotDuplicatedButRemainsAvailableForHistory()
    {
        var project = CreateProject("MossAgent");
        var pinned = CreateTask(project.Id, "置顶会话") with { IsPinned = true };
        var ordinary = CreateTask(project.Id, "普通会话");
        var navigation = new WorkspaceProjectNavigationViewModel(project);

        navigation.SetTasks([pinned, ordinary]);

        Assert.Equal(ordinary.Id, Assert.Single(navigation.Tasks).Task.Id);
        Assert.Equal(pinned, navigation.FindTask(pinned.Id));
        Assert.Equal("2 个任务", navigation.ProjectTaskSummary);
        navigation.ApplyFilter("置顶");
        Assert.Empty(navigation.Tasks);
        Assert.False(navigation.IsVisible);
    }

    /// <summary>
    /// 创建用于侧栏导航测试的项目资料。
    /// </summary>
    private static ProjectProfile CreateProject(string name) => new(
        Guid.NewGuid(), name, $"C:\\{name}", [$"C:\\{name}"], DateTimeOffset.UtcNow);

    /// <summary>
    /// 创建用于侧栏导航测试的任务。
    /// </summary>
    private static AgentTask CreateTask(Guid projectId, string title)
    {
        var now = DateTimeOffset.UtcNow;
        return new AgentTask(
            Guid.NewGuid(), projectId, title, ApprovalPolicy.AskEveryTime,
            AgentTaskStatus.Completed, now, now, null);
    }
}
