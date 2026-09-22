using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceTaskSearchViewModelTests
{
    [Fact]
    public void Query_FiltersAndClearRestoresTasks()
    {
        var projectId = Guid.NewGuid();
        var search = new WorkspaceTaskSearchViewModel();
        search.SetItems([
            CreateTask(projectId, "修复浏览器操作"),
            CreateTask(projectId, "实现模型设置")
        ]);

        search.Query = "浏览器";

        Assert.Single(search.Results);
        Assert.Equal("修复浏览器操作", search.Results[0].Title);
        Assert.True(search.HasQuery);

        search.ClearCommand.Execute(null);

        Assert.Equal(2, search.Results.Count);
        Assert.False(search.HasQuery);
    }

    [Fact]
    public void Upsert_RespectsActiveFilter()
    {
        var projectId = Guid.NewGuid();
        var search = new WorkspaceTaskSearchViewModel { Query = "新增" };

        search.Upsert(CreateTask(projectId, "新增搜索功能"));
        search.Upsert(CreateTask(projectId, "修复终端"));

        Assert.Single(search.Results);
        Assert.Equal("新增搜索功能", search.Results[0].Title);
    }

    private static AgentTask CreateTask(Guid projectId, string title)
    {
        var now = DateTimeOffset.UtcNow;
        return new AgentTask(
            Guid.NewGuid(), projectId, title, ApprovalPolicy.AskEveryTime,
            AgentTaskStatus.Completed, now, now, null);
    }
}
