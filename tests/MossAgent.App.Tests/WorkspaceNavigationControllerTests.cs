using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceNavigationControllerTests
{
    [Fact]
    public void BackAndForward_UseTheCurrentSelectionAndNotifyAvailability()
    {
        var project = Guid.NewGuid();
        var first = ((Guid?)project, (Guid?)Guid.NewGuid());
        var second = ((Guid?)project, (Guid?)Guid.NewGuid());
        var selection = first;
        var controller = new WorkspaceNavigationController(() => selection, () => true, target => selection = target);
        var changes = 0;
        controller.BackCommand.CanExecuteChanged += (_, _) => changes++;
        controller.Record(second);
        selection = second;
        controller.BackCommand.Execute(null);
        Assert.Equal(first, selection);
        Assert.True(controller.ForwardCommand.CanExecute(null));
        controller.ForwardCommand.Execute(null);
        Assert.Equal(second, selection);
        Assert.Equal(3, changes);
    }

    [Fact]
    public void LockedNavigation_RejectsDirectExecutionWithoutConsumingHistory()
    {
        var first = ((Guid?)Guid.NewGuid(), (Guid?)Guid.NewGuid());
        var selection = first;
        var allowed = true;
        var controller = new WorkspaceNavigationController(() => selection, () => allowed, target => selection = target);
        var second = (first.Item1, (Guid?)Guid.NewGuid());
        controller.Record(second);
        selection = second;
        allowed = false;
        controller.NotifyAvailabilityChanged();
        Assert.False(controller.BackCommand.CanExecute(null));
        controller.BackCommand.Execute(null);
        Assert.Equal(second, selection);
        allowed = true;
        controller.BackCommand.Execute(null);
        Assert.Equal(first, selection);
    }

    [Fact]
    public void RemovingATask_PrunesTheSameHistoryUsedByTheCommands()
    {
        var first = ((Guid?)Guid.NewGuid(), (Guid?)Guid.NewGuid());
        var selection = first;
        var controller = new WorkspaceNavigationController(() => selection, () => true, target => selection = target);
        var second = (first.Item1, (Guid?)Guid.NewGuid());
        controller.Record(second);
        selection = second;
        controller.BackCommand.Execute(null);
        controller.RemoveTask(second.Item2!.Value);
        Assert.False(controller.ForwardCommand.CanExecute(null));
        controller.ForwardCommand.Execute(null);
        Assert.Equal(first, selection);
    }
}
