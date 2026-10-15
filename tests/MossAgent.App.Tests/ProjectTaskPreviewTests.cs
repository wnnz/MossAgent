using System.Collections.Specialized;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ProjectTaskPreviewTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(12)]
    public void PreviewLimitsOnlyPresentationAndDisablesUnnecessaryExpansion(int count)
    {
        var navigation = CreateNavigation(count);
        var preview = navigation.TaskPreview;
        Assert.Equal(count, navigation.Tasks.Count);
        Assert.Equal(Math.Min(count, 5), preview.Items.Count);
        Assert.Equal(count > 5, preview.CanToggle);
        Assert.Equal(count > 5, preview.ToggleCommand.CanExecute(null));
        Assert.Equal("展开显示", preview.ToggleLabel);
        Assert.All(preview.Items, row => Assert.Contains(row, navigation.Tasks));
        if (count <= 5)
        {
            preview.ToggleCommand.Execute(null);
            Assert.False(preview.IsShowingAll);
        }
    }

    [Fact]
    public void ExpansionAndCollapseKeepRowsAndDoNotResetEitherCollection()
    {
        var navigation = CreateNavigation(8);
        var original = navigation.Tasks.ToArray();
        var actions = new List<NotifyCollectionChangedAction>();
        navigation.TaskPreview.Items.CollectionChanged += (_, args) => actions.Add(args.Action);
        navigation.Tasks.CollectionChanged += (_, args) => Assert.Fail("Backing rows changed");
        navigation.TaskPreview.ToggleCommand.Execute(null);
        Assert.True(navigation.TaskPreview.IsShowingAll);
        Assert.Equal("收起显示", navigation.TaskPreview.ToggleLabel);
        Assert.Equal(original, navigation.TaskPreview.Items);
        navigation.IsExpanded = false;
        navigation.IsExpanded = true;
        Assert.Equal(8, navigation.TaskPreview.Items.Count);
        navigation.TaskPreview.ToggleCommand.Execute(null);
        Assert.Equal(original.Take(5), navigation.TaskPreview.Items);
        Assert.Equal(original, navigation.Tasks);
        Assert.DoesNotContain(NotifyCollectionChangedAction.Reset, actions);
        Assert.DoesNotContain(NotifyCollectionChangedAction.Replace, actions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SearchShowsEveryMatchAndRestoresTheUsersExpansionChoice(bool expanded)
    {
        var navigation = CreateNavigation(8);
        if (expanded) navigation.TaskPreview.ToggleCommand.Execute(null);
        navigation.ApplyFilter("task");
        Assert.Equal(8, navigation.TaskPreview.Items.Count);
        Assert.False(navigation.TaskPreview.CanToggle);
        navigation.TaskPreview.ToggleCommand.Execute(null);
        Assert.Equal(expanded, navigation.TaskPreview.IsShowingAll);
        navigation.ApplyFilter("task 7");
        Assert.Equal("task 7", Assert.Single(navigation.TaskPreview.Items).Task.Title);
        navigation.ApplyFilter("absent");
        Assert.Empty(navigation.TaskPreview.Items);
        Assert.False(navigation.IsVisible);
        navigation.ApplyFilter(string.Empty);
        Assert.Equal(expanded ? 8 : 5, navigation.TaskPreview.Items.Count);
        Assert.True(navigation.IsVisible);
    }

    [Fact]
    public void SelectingHiddenTaskRevealsItButRepeatingSelectionRespectsManualCollapse()
    {
        var navigation = CreateNavigation(8);
        var task = navigation.Tasks[7].Task;
        navigation.SetSelectedTask(task.Id);
        Assert.True(navigation.TaskPreview.IsShowingAll);
        Assert.True(navigation.TaskPreview.Items[7].IsSelected);
        navigation.TaskPreview.ToggleCommand.Execute(null);
        navigation.SetSelectedTask(task.Id);
        navigation.ApplyFilter(string.Empty);
        Assert.Equal(5, navigation.TaskPreview.Items.Count);
        Assert.True(navigation.Tasks[7].IsSelected);
        navigation.SetSelectedTask(null);
        navigation.SetSelectedTask(task.Id);
        Assert.Equal(8, navigation.TaskPreview.Items.Count);
    }

    [Fact]
    public void SelectionBeforeLoadingAndWhileFilteringIsRevealedWhenRowsBecomeAvailable()
    {
        var source = CreateNavigation(8);
        var task = source.Tasks[7].Task;
        var navigation = new WorkspaceProjectNavigationViewModel(source.Project);
        navigation.SetSelectedTask(task.Id);
        navigation.SetTasks(source.TaskSnapshot);
        Assert.Equal(8, navigation.TaskPreview.Items.Count);
        Assert.True(navigation.TaskPreview.Items[7].IsSelected);
        navigation.TaskPreview.ToggleCommand.Execute(null);
        navigation.SetSelectedTask(null);
        navigation.ApplyFilter("absent");
        navigation.SetSelectedTask(task.Id);
        Assert.Empty(navigation.TaskPreview.Items);
        navigation.ApplyFilter(string.Empty);
        Assert.Equal(8, navigation.TaskPreview.Items.Count);
        Assert.True(navigation.TaskPreview.Items[7].IsSelected);
    }

    [Fact]
    public void InvalidPinnedAndAlreadyVisibleSelectionsDoNotExpandThePreview()
    {
        var navigation = CreateNavigation(8);
        var pinned = navigation.Tasks[7].Task with { IsPinned = true };
        navigation.UpsertTask(pinned);
        foreach (var taskId in new Guid?[] { null, Guid.NewGuid(), pinned.Id, navigation.Tasks[0].Task.Id })
        {
            navigation.SetSelectedTask(taskId);
            Assert.False(navigation.TaskPreview.IsShowingAll);
            Assert.Equal(5, navigation.TaskPreview.Items.Count);
        }
        Assert.Equal(pinned, navigation.FindTask(pinned.Id));
    }

    [Fact]
    public void RefreshIncludesUpdatedRowsAndThresholdChangesWithoutStaleHiddenTasks()
    {
        var navigation = CreateNavigation(6);
        var retained = navigation.Tasks[0];
        var hidden = navigation.Tasks[5].Task;
        var properties = new List<string?>();
        navigation.TaskPreview.PropertyChanged += (_, args) => properties.Add(args.PropertyName);
        navigation.UpsertTask(hidden with { Title = "renamed" });
        Assert.Equal("renamed", navigation.TaskPreview.Items[0].Task.Title);
        Assert.Contains(retained, navigation.TaskPreview.Items);
        navigation.RemoveTask(hidden.Id);
        Assert.False(navigation.TaskPreview.CanToggle);
        Assert.Contains(nameof(WorkspaceProjectTaskPreviewViewModel.CanToggle), properties);
        Assert.DoesNotContain(navigation.TaskPreview.Items, row => row.Task.Id == hidden.Id);
        navigation.UpsertTask(hidden);
        Assert.True(navigation.TaskPreview.CanToggle);
        Assert.Equal(hidden.Id, navigation.TaskPreview.Items[0].Task.Id);
    }

    [Fact]
    public void EquivalentRefreshRetainsPreviewRowsWithoutCollectionNotifications()
    {
        var navigation = CreateNavigation(8);
        var notifications = 0;
        navigation.TaskPreview.Items.CollectionChanged += (_, _) => notifications++;
        var original = navigation.TaskPreview.Items.ToArray();
        for (var index = 0; index < 100; index++) navigation.ApplyFilter(string.Empty);
        Assert.Equal(0, notifications);
        Assert.Equal(original, navigation.TaskPreview.Items);
    }

    [Fact]
    public void ProjectGroupsOwnIndependentPreviewState()
    {
        var first = CreateNavigation(8);
        var second = CreateNavigation(8);
        first.TaskPreview.ToggleCommand.Execute(null);
        Assert.Equal(8, first.TaskPreview.Items.Count);
        Assert.Equal(5, second.TaskPreview.Items.Count);
    }

    private static WorkspaceProjectNavigationViewModel CreateNavigation(int count)
    {
        var project = new ProjectProfile(Guid.NewGuid(), "project", "C:\\fixture", ["C:\\fixture"], DateTimeOffset.UnixEpoch);
        var navigation = new WorkspaceProjectNavigationViewModel(project);
        navigation.SetTasks(Enumerable.Range(0, count).Select(index => new AgentTask(
            Guid.NewGuid(), project.Id, $"task {index}", ApprovalPolicy.ReadOnly, AgentTaskStatus.Completed,
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(count - index), null)));
        return navigation;
    }
}
