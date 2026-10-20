using System.Net;
using Avalonia.Headless;
using MossAgent.App.Resources;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class PullRequestLinkLifecycleTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData("navigate")]
    [InlineData("close")]
    [InlineData("cancel")]
    [InlineData("dispose")]
    public Task InflightReadsDrainWithoutLateResultsOrAutomaticReplay(string action) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestLinkTestSetup.CreateAsync();
        var model = fixture.Main.Pages.PullRequests!;
        var scope = model.SelectedRepository!.Scope;
        var taskId = await fixture.ReviewSessions.GetOrCreateAsync(scope, fixture.Token);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Transport.RespondAsync = async (_, _) =>
        {
            entered.TrySetResult(); await release.Task.WaitAsync(fixture.Token);
            return new(HttpStatusCode.OK) { Content = new StringContent(PullRequestTestData.Item()) };
        };
        model.Link.ShowCommand.Execute(null); model.Link.Account = Assert.Single(model.Accounts);
        model.Link.Url = "https://github.com/fixture/repo/pull/7";
        var running = model.Link.OpenCommand.ExecuteAsync(null);
        Task disposal = Task.CompletedTask;
        try
        {
            await entered.Task.WaitAsync(fixture.Token);
            Assert.True(model.IsBusy); Assert.False(model.LoadCommand.CanExecute(null));
            if (action == "navigate") fixture.Main.OpenSettingsCommand.Execute(null);
            else if (action == "close") model.Link.CloseCommand.Execute(null);
            else if (action == "cancel") model.Link.CancelCommand.Execute(null);
            else disposal = model.DisposeAsync().AsTask();
            Assert.False(running.IsCompleted); Assert.False(model.Link.ShowCommand.CanExecute(null));
            if (action == "dispose") Assert.False(disposal.IsCompleted);
        }
        finally { release.TrySetResult(); await running; await disposal; }
        Assert.Null(model.Detail); Assert.False(model.Inspection.IsActive);
        Assert.Equal(1, fixture.Transport.RequestCount); Assert.Equal(1, fixture.Transport.DisposedHandlers);
        var record = Assert.Single(await fixture.Audit.GetRecentAsync(taskId, 10, fixture.Token));
        Assert.False(record.IsSuccess); Assert.NotNull(record.CompletedAt);
        if (action == "dispose") return;
        Assert.False(model.IsBusy); Assert.Equal(PullRequestText.Get("Cancelled"), model.Link.Status);
        if (!model.Link.IsOpen) model.Link.ShowCommand.Execute(null);
        fixture.Transport.RespondAsync = PullRequestInspectionUiFixture.RespondAsync;
        Assert.Equal(1, fixture.Transport.RequestCount);
        await model.Link.OpenCommand.ExecuteAsync(null);
        Assert.False(model.Link.IsOpen, model.Link.Status); Assert.NotNull(model.Detail);
        Assert.Equal(2, fixture.Transport.RequestCount);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task OpeningAnotherLinkWaitsForPriorInspectionCleanupBeforeRequestingItsDetail() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestInspectionUiFixture.CreateAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleared = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var linkedRequests = 0;
        fixture.Source.Transport.RespondAsync = async (request, _) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/42", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref linkedRequests);
                return new(HttpStatusCode.OK) { Content = new StringContent(PullRequestTestData.Item(42)) };
            }
            entered.TrySetResult(); await release.Task.WaitAsync(fixture.Token);
            return new(HttpStatusCode.OK) { Content = new StringContent(PullRequestTestData.Item()) };
        };
        var previous = fixture.Model.Inspection.Files.LoadCommand.ExecuteAsync(null);
        await entered.Task.WaitAsync(fixture.Token);
        void ObserveClear(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
        { if (args.PropertyName == "IsActive" && !fixture.Model.Inspection.IsActive) cleared.TrySetResult(); }
        fixture.Model.Inspection.PropertyChanged += ObserveClear;
        fixture.Model.Link.ShowCommand.Execute(null); fixture.Model.Link.Account = Assert.Single(fixture.Model.Accounts);
        fixture.Model.Link.Url = "https://github.com/fixture/repo/pull/42";
        var opening = fixture.Model.Link.OpenCommand.ExecuteAsync(null);
        try
        {
            await cleared.Task.WaitAsync(TimeSpan.FromSeconds(20), fixture.Token);
            Assert.Equal(0, Volatile.Read(ref linkedRequests)); Assert.False(opening.IsCompleted);
        }
        finally { fixture.Model.Inspection.PropertyChanged -= ObserveClear; release.TrySetResult(); await previous; await opening; }
        Assert.Equal(42, fixture.Model.Detail?.Number); Assert.Empty(fixture.Model.Inspection.Files.Items);
        Assert.True(fixture.Model.Inspection.IsActive); Assert.Equal(1, linkedRequests);
    }, TestContext.Current.CancellationToken);
}
