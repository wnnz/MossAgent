using Avalonia.Headless;
using MossAgent.App.Resources;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class AutomationRowMutationTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task CancelledToggleOrRemoval_PassesTokenKeepsOwnershipAndDoesNotMutate(bool removal) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var token = CancellationToken.None;
        var repository = new AutomationListProbeRepository(fixture.Automations)
        {
            Write = (_, cancellation) => { token = cancellation; return completion.Task; },
            Archive = (_, _, cancellation) => { token = cancellation; return completion.Task; }
        };
        using var page = AutomationSaveOwnershipTests.Page(fixture, repository);
        await fixture.Automations.SaveAsync(AutomationListOwnershipTests.Schedule(fixture), fixture.Token);
        await page.ReloadAsync();
        var row = Assert.Single(page.Items);
        if (removal) page.RemoveCommand.Execute(row);
        var command = removal ? page.ConfirmDeleteCommand : page.ToggleCommand;
        var parameter = removal ? null : row;
        var pending = command.ExecuteAsync(parameter);
        try
        {
            Assert.True(command.CanBeCanceled);
            page.Operation.CancelCommand.Execute(null);
            Assert.True(token.IsCancellationRequested);
            Assert.True(page.IsBusy);
            Assert.False(page.CreateCommand.CanExecute(null));
            await command.ExecuteAsync(parameter);
            Assert.Equal(1, repository.WriteCount);
        }
        finally { completion.TrySetResult(false); await pending; }
        Assert.False(page.IsBusy);
        Assert.Equal(AutomationText.Get("OperationCancelled"), page.Error);
        Assert.False(Assert.Single(await fixture.Automations.GetAllAsync(fixture.Token)).IsEnabled);
        Assert.Empty(fixture.Server.Requests);
        Assert.Equal(removal, page.IsDeleteOpen);
    }, TestContext.Current.CancellationToken);
}
