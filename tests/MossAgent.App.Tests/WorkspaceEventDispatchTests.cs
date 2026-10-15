using System.Collections.ObjectModel;
using MossAgent.App.ViewModels;
using MossAgent.Application.Agent;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceEventDispatchTests
{
    [Fact]
    public async Task WithoutAnApplication_EventsDoNotWaitForAnUnstartedDispatcher()
    {
        var assistant = new ChatMessageViewModel("MossAgent", string.Empty);
        var messages = new ObservableCollection<ChatMessageViewModel> { assistant };
        var called = false;
        var modelEvent = new AgentTextEvent("headless text");

        var result = await WorkspaceAgentEventPresenter.PresentAsync(modelEvent, assistant, messages,
            _ => { }, presented => called = ReferenceEquals(presented, modelEvent))
            .WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.Same(assistant, result);
        Assert.Equal("headless text", assistant.Content);
        Assert.True(called);
    }
}
