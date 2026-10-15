using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceNavigationHistoryTests
{
    [Fact]
    public void GoBackAndForward_RestorePreviousAndNextSelection()
    {
        var history = new WorkspaceNavigationHistory();
        var projectId = Guid.NewGuid();
        var firstTaskId = Guid.NewGuid();
        var secondTaskId = Guid.NewGuid();
        var first = (projectId, (Guid?)firstTaskId);
        var second = (projectId, (Guid?)secondTaskId);

        history.Navigate(first, second);

        Assert.True(history.TryGoBack(second, out var previous));
        Assert.Equal(first, previous);
        Assert.True(history.TryGoForward(first, out var next));
        Assert.Equal(second, next);
    }

    [Fact]
    public void NavigateAfterGoingBack_ClearsForwardHistory()
    {
        var history = new WorkspaceNavigationHistory();
        var projectId = Guid.NewGuid();
        var first = (projectId, (Guid?)Guid.NewGuid());
        var second = (projectId, (Guid?)Guid.NewGuid());
        var third = (projectId, (Guid?)Guid.NewGuid());

        history.Navigate(first, second);
        Assert.True(history.TryGoBack(second, out var previous));
        history.Navigate(previous, third);

        Assert.False(history.CanGoForward);
        Assert.True(history.CanGoBack);
    }

    [Fact]
    public void RemoveTask_PrunesBackAndForwardEntries()
    {
        var history = new WorkspaceNavigationHistory();
        var projectId = Guid.NewGuid();
        var first = (projectId, (Guid?)Guid.NewGuid());
        var second = (projectId, (Guid?)Guid.NewGuid());

        history.Navigate(first, second);
        Assert.True(history.TryGoBack(second, out _));
        history.RemoveTask(second.Item2!.Value);

        Assert.False(history.CanGoForward);
    }
}
