using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceAgentPickerLifetimeTests
{
    [Fact]
    public async Task Loading_BlocksStaleActionsAndDoesNotCancelAnAcceptedReadOnReentry()
    {
        using var fixture = await WorkspaceAgentPickerTestFixture.CreateAsync();
        fixture.Picker.SelectCommand.Execute(fixture.First);
        var gate = Gate();
        fixture.Repository.Loader = _ => gate.Task;
        var pending = fixture.Picker.RefreshCommand.ExecuteAsync(null);
        var token = fixture.Repository.ReadToken;
        Assert.True(fixture.Picker.IsLoading);
        Assert.False(fixture.Picker.CanUseSelection);
        Assert.False(fixture.Picker.SelectCommand.CanExecute(fixture.Second));
        fixture.Picker.SelectCommand.Execute(fixture.Second);
        await fixture.Picker.RefreshCommand.ExecuteAsync(null);
        Assert.Equal(fixture.First.Id, fixture.Picker.SelectedProfile?.Id);
        Assert.False(token.IsCancellationRequested);
        gate.SetResult([fixture.First, fixture.Second]);
        await pending;
        Assert.True(fixture.Picker.CanUseSelection);
    }

    [Fact]
    public async Task AnExplicitModelChoiceDuringARead_IsNotOverwrittenByTheLateAgentSnapshot()
    {
        using var fixture = await WorkspaceAgentPickerTestFixture.CreateAsync();
        fixture.Picker.Select(fixture.First);
        var gate = Gate();
        fixture.Repository.Loader = _ => gate.Task;
        var pending = fixture.Picker.ReloadAsync(TestContext.Current.CancellationToken);

        Assert.True(fixture.Models.TrySelect(fixture.Second.ProviderId, fixture.Second.ModelProfileId));
        gate.SetResult([fixture.First, fixture.Second]);
        await pending;

        Assert.Null(fixture.Picker.SelectedProfile);
        Assert.Equal(fixture.Second.ModelProfileId, fixture.Models.SelectedItem?.Model.Id);
        Assert.True(fixture.Picker.CanUseSelection);
    }

    [Fact]
    public async Task SupersededReads_AreCancelledAndCannotReplaceTheNewestConfiguration()
    {
        using var fixture = await WorkspaceAgentPickerTestFixture.CreateAsync();
        fixture.Picker.Select(fixture.First);
        var gate = Gate();
        fixture.Repository.Loader = _ => gate.Task;
        var older = fixture.Picker.ReloadAsync(TestContext.Current.CancellationToken);
        var olderToken = fixture.Repository.ReadToken;
        var current = fixture.First with { Name = "current", SystemPrompt = "current-prompt", ReasoningEffort = "ultra" };
        fixture.Repository.Loader = _ => Task.FromResult<IReadOnlyList<AgentProfile>>([current, fixture.Second]);

        await fixture.Picker.ReloadAsync(TestContext.Current.CancellationToken);
        await older;
        gate.SetResult([fixture.First]);

        Assert.True(olderToken.IsCancellationRequested);
        Assert.Equal(current, fixture.Picker.SelectedProfile);
        Assert.Equal("ultra", fixture.Models.EffectiveReasoningEffort);
        Assert.Equal(2, fixture.Picker.Items.Count);
        Assert.True(fixture.Picker.CanUseSelection);
    }

    [Fact]
    public async Task Disposal_CancelsTheReadAndRejectsLatePublicationAndFurtherReads()
    {
        using var fixture = await WorkspaceAgentPickerTestFixture.CreateAsync();
        var original = fixture.Picker.Items.ToArray();
        var gate = Gate();
        fixture.Repository.Loader = _ => gate.Task;
        var pending = fixture.Picker.ReloadAsync(TestContext.Current.CancellationToken);
        var token = fixture.Repository.ReadToken;
        fixture.Picker.Dispose();
        var notifications = 0;
        fixture.Picker.PropertyChanged += (_, _) => notifications++;
        var reads = fixture.Repository.ReadCalls;
        await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        gate.SetResult([]);
        await fixture.Picker.ReloadAsync(TestContext.Current.CancellationToken);

        Assert.True(token.IsCancellationRequested);
        Assert.Equal(original, fixture.Picker.Items);
        Assert.Equal(reads, fixture.Repository.ReadCalls);
        Assert.Equal(0, notifications);
        Assert.False(fixture.Picker.CanUseSelection);
        Assert.False(fixture.Picker.RefreshCommand.CanExecute(null));
    }

    [Fact]
    public async Task NoncooperativeReadTimeout_KeepsTheSnapshotAndCanBeRetried()
    {
        using var fixture = await WorkspaceAgentPickerTestFixture.CreateAsync(TimeSpan.FromMilliseconds(50));
        fixture.Picker.Select(fixture.First);
        var original = fixture.Picker.Items.ToArray();
        var gate = Gate();
        fixture.Repository.Loader = _ => gate.Task;

        await fixture.Picker.ReloadAsync(TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.True(fixture.Repository.ReadToken.IsCancellationRequested);
        Assert.True(fixture.Picker.HasLoadError);
        Assert.False(fixture.Picker.IsLoading);
        Assert.False(fixture.Picker.CanUseSelection);
        Assert.False(fixture.Picker.IsSelectedUnavailable);
        Assert.Equal(original, fixture.Picker.Items);
        gate.SetResult([]);
        fixture.Repository.Loader = null;
        await fixture.Picker.RefreshCommand.ExecuteAsync(null);
        Assert.True(fixture.Picker.CanUseSelection);
        Assert.False(fixture.Picker.HasCatalogNotice);
    }

    [Fact]
    public async Task CommandCancellation_ReachesTheReadAndAllowsARetryWithoutResettingTheSelection()
    {
        using var fixture = await WorkspaceAgentPickerTestFixture.CreateAsync();
        fixture.Picker.Select(fixture.First);
        var gate = Gate();
        fixture.Repository.Loader = _ => gate.Task;
        var pending = fixture.Picker.RefreshCommand.ExecuteAsync(null);
        var token = fixture.Repository.ReadToken;

        fixture.Picker.RefreshCommand.Cancel();
        await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.True(token.IsCancellationRequested);
        Assert.True(fixture.Picker.HasLoadError);
        Assert.Equal(fixture.First.Id, fixture.Picker.SelectedProfile?.Id);
        gate.SetResult([]);
        fixture.Repository.Loader = null;
        await fixture.Picker.RefreshCommand.ExecuteAsync(null);
        Assert.True(fixture.Picker.CanUseSelection);
    }

    private static TaskCompletionSource<IReadOnlyList<AgentProfile>> Gate() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
