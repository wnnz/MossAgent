using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ConfigurationDraftNotificationTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task FailedOlderMutation_DoesNotPublishIntoANewDraft(bool provider, bool delete)
    {
        var repository = ConfigurationMutationFailureTests.Repository();
        using var providers = new ProviderSettingsViewModel(repository);
        using var models = new ModelSettingsViewModel(repository, new FixedModelDiscoveryService([]), providers);
        providers.EditProviderCommand.Execute(repository.Store.Providers[0]);
        models.EditProviderModelCommand.Execute(repository.Store.Models[0]);
        var command = provider
            ? delete ? providers.DeleteCommand : providers.SaveCommand
            : delete ? models.DeleteModelCommand : models.SaveModelCommand;
        if (provider && delete) await command.ExecuteAsync(null);
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.BeforeWriteAsync = token => pending.Task.WaitAsync(token);
        repository.FailingOperation = (delete ? "delete-" : "save-") + (provider ? "provider" : "model");
        var mutation = command.ExecuteAsync(null);
        Assert.True(providers.Catalog.Mutations.IsRunning);
        if (provider)
        {
            providers.OpenCreateFormCommand.Execute(null);
            providers.Name = "new-provider-draft";
        }
        else
        {
            models.OpenCreateFormCommand.Execute(null);
            models.ModelId = "new-model-draft";
        }
        var status = provider ? providers.Status : models.Status;
        try { pending.SetResult(); await mutation; }
        finally { pending.TrySetResult(); await mutation; }

        Assert.Equal(status, provider ? providers.Status : models.Status);
        Assert.False(provider ? providers.HasValidationError : models.HasStatusError);
        Assert.True(provider ? providers.IsFormOpen : models.IsFormOpen);
        Assert.Equal(provider ? "new-provider-draft" : "new-model-draft", provider ? providers.Name : models.ModelId);
        Assert.Single(repository.Store.Providers);
        Assert.Single(repository.Store.Models);
    }
}
