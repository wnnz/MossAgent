using MossAgent.App.Resources;
using MossAgent.App.ViewModels;
using MossAgent.Tools.Abstractions.Browser;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class BrowserTakeoverViewModelTests
{
    [Fact]
    public void NoSelectedTask_HidesPaneAndDisablesCommands()
    {
        using var viewModel = new BrowserTakeoverViewModel(new StubBrowserTakeoverController());

        Assert.False(viewModel.HasControlState);
        Assert.False(viewModel.HandToUserCommand.CanExecute(null));
        Assert.False(viewModel.ReturnControlCommand.CanExecute(null));
    }

    [Fact]
    public void AttachedAutomatedTask_ShowsLabelAndEnablesHandover()
    {
        var controller = new StubBrowserTakeoverController();
        var taskId = Guid.NewGuid();
        controller.SetState(taskId, BrowserControlState.Automated);
        using var viewModel = new BrowserTakeoverViewModel(controller);

        viewModel.AttachTask(taskId);

        Assert.True(viewModel.HasControlState);
        Assert.Equal(BrowserControlState.Automated, viewModel.State);
        Assert.Equal(WorkspaceText.Get("BrowserControlAutomated"), viewModel.StateLabel);
        Assert.True(viewModel.HandToUserCommand.CanExecute(null));
        Assert.False(viewModel.ReturnControlCommand.CanExecute(null));
        Assert.False(viewModel.HasStatus);
    }

    [Fact]
    public async Task HandToUserCommand_TransitionsStateAndGatesCommands()
    {
        var controller = new StubBrowserTakeoverController();
        var taskId = Guid.NewGuid();
        controller.SetState(taskId, BrowserControlState.Automated);
        using var viewModel = new BrowserTakeoverViewModel(controller);
        viewModel.AttachTask(taskId);

        await viewModel.HandToUserCommand.ExecuteAsync(null);

        Assert.Equal((taskId, "takeover"), Assert.Single(controller.Calls));
        Assert.Equal(BrowserControlState.HandedToUser, viewModel.State);
        Assert.Equal(WorkspaceText.Get("BrowserControlHandedToUser"), viewModel.StateLabel);
        Assert.False(viewModel.HandToUserCommand.CanExecute(null));
        Assert.True(viewModel.ReturnControlCommand.CanExecute(null));
    }

    [Fact]
    public async Task ReturnControlCommand_RestoresAutomation()
    {
        var controller = new StubBrowserTakeoverController();
        var taskId = Guid.NewGuid();
        controller.SetState(taskId, BrowserControlState.HandedToUser);
        using var viewModel = new BrowserTakeoverViewModel(controller);
        viewModel.AttachTask(taskId);

        await viewModel.ReturnControlCommand.ExecuteAsync(null);

        Assert.Equal((taskId, "return"), Assert.Single(controller.Calls));
        Assert.Equal(BrowserControlState.Automated, viewModel.State);
        Assert.True(viewModel.HandToUserCommand.CanExecute(null));
        Assert.False(viewModel.ReturnControlCommand.CanExecute(null));
        Assert.False(viewModel.HasStatus);
    }

    [Fact]
    public async Task FailedReturn_StaysHandedToUserAndShowsStatus()
    {
        var controller = new StubBrowserTakeoverController();
        controller.ReturnBehavior = (taskId, _) =>
        {
            controller.SetState(taskId, BrowserControlState.HandedToUser);
            return Task.FromResult(BrowserControlState.HandedToUser);
        };
        var taskId = Guid.NewGuid();
        controller.SetState(taskId, BrowserControlState.HandedToUser);
        using var viewModel = new BrowserTakeoverViewModel(controller);
        viewModel.AttachTask(taskId);

        await viewModel.ReturnControlCommand.ExecuteAsync(null);

        Assert.Equal(BrowserControlState.HandedToUser, viewModel.State);
        Assert.True(viewModel.HasStatus);
        Assert.Equal(WorkspaceText.Get("BrowserReturnFailed"), viewModel.Status);
        Assert.True(viewModel.ReturnControlCommand.CanExecute(null));
    }

    [Fact]
    public void ReturningToAutomatedState_DisablesBothCommands()
    {
        var controller = new StubBrowserTakeoverController();
        var taskId = Guid.NewGuid();
        controller.SetState(taskId, BrowserControlState.ReturningToAutomated);
        using var viewModel = new BrowserTakeoverViewModel(controller);

        viewModel.AttachTask(taskId);

        Assert.Equal(
            WorkspaceText.Get("BrowserControlReturningToAutomated"), viewModel.StateLabel);
        Assert.False(viewModel.HandToUserCommand.CanExecute(null));
        Assert.False(viewModel.ReturnControlCommand.CanExecute(null));
    }

    [Fact]
    public void DisconnectedState_DisablesCommandsAndShowsLabel()
    {
        var controller = new StubBrowserTakeoverController();
        var taskId = Guid.NewGuid();
        controller.SetState(taskId, BrowserControlState.Automated);
        using var viewModel = new BrowserTakeoverViewModel(controller);
        viewModel.AttachTask(taskId);

        controller.MarkDisconnected(taskId);

        Assert.Equal(BrowserControlState.Disconnected, viewModel.State);
        Assert.Equal(WorkspaceText.Get("BrowserControlDisconnected"), viewModel.StateLabel);
        Assert.False(viewModel.HandToUserCommand.CanExecute(null));
        Assert.False(viewModel.ReturnControlCommand.CanExecute(null));
    }

    [Fact]
    public void ReleaseEvent_HidesPane()
    {
        var controller = new StubBrowserTakeoverController();
        var taskId = Guid.NewGuid();
        controller.SetState(taskId, BrowserControlState.HandedToUser);
        using var viewModel = new BrowserTakeoverViewModel(controller);
        viewModel.AttachTask(taskId);

        controller.RemoveState(taskId);

        Assert.False(viewModel.HasControlState);
        Assert.False(viewModel.HandToUserCommand.CanExecute(null));
    }

    [Fact]
    public void EventsForOtherTasks_AreIgnored()
    {
        var controller = new StubBrowserTakeoverController();
        var taskId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        controller.SetState(taskId, BrowserControlState.Automated);
        using var viewModel = new BrowserTakeoverViewModel(controller);
        viewModel.AttachTask(taskId);

        controller.SetState(otherId, BrowserControlState.HandedToUser);

        Assert.Equal(BrowserControlState.Automated, viewModel.State);
        Assert.True(viewModel.HandToUserCommand.CanExecute(null));
    }

    [Fact]
    public void Dispose_UnsubscribesAndDisablesCommands()
    {
        var controller = new StubBrowserTakeoverController();
        var taskId = Guid.NewGuid();
        controller.SetState(taskId, BrowserControlState.Automated);
        var viewModel = new BrowserTakeoverViewModel(controller);
        viewModel.AttachTask(taskId);

        viewModel.Dispose();
        controller.SetState(taskId, BrowserControlState.HandedToUser);

        Assert.False(viewModel.HasControlState);
        Assert.False(viewModel.HandToUserCommand.CanExecute(null));
        Assert.False(viewModel.ReturnControlCommand.CanExecute(null));
    }
}
