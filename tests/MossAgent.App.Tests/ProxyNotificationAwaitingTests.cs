using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ProxyNotificationAwaitingTests
{
    [Fact]
    public async Task Mutation_AwaitsEachCatalogSubscriberBeforeReleasingOwnership()
    {
        var repository = new ProxyMutationProbeRepository();
        using var viewModel = new ProxySettingsViewModel(repository, new RecordingProxyConnectionTester());
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondNotified = false;
        viewModel.ProxiesChanged += () => completion.Task;
        viewModel.ProxiesChanged += () => { secondNotified = true; return Task.CompletedTask; };
        viewModel.OpenCreateFormCommand.Execute(null);
        viewModel.Name = "synthetic proxy";
        var pending = viewModel.SaveCommand.ExecuteAsync(null);
        try
        {
            Assert.False(pending.IsCompleted);
            Assert.False(secondNotified);
            Assert.True(viewModel.IsBusy);
            Assert.False(viewModel.SaveCommand.CanExecute(null));
        }
        finally { completion.TrySetResult(); await pending; }

        Assert.True(secondNotified);
        Assert.False(viewModel.IsBusy);
        Assert.Single(repository.Proxies);
    }
}
