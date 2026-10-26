using System.Net;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Resources;
using MossAgent.App.Views;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class PullRequestMergeViewModelTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    private static string MergeAccepted => $"{{\"sha\":\"{new string('c', 40)}\",\"merged\":true,\"message\":\"merged\"}}";

    private static Task<HttpResponseMessage> Detail(bool put) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent(put ? PullRequestTestData.Merged() : PullRequestTestData.Item()) });

    [Fact]
    public Task ReadOnlyGrantShowsTheLocalizedHintAndKeepsTheMergeControlsHidden() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestInspectionUiFixture.CreateAsync();
        var model = fixture.Model.Inspection;
        Assert.True(model.Merge.IsActive);
        Assert.False(model.Merge.CanWrite);
        Assert.False(model.Merge.CanMerge);
        Assert.True(model.Merge.ShowReadOnlyHint);
        Assert.False(model.Merge.ShowDraftHint);
        Assert.False(model.Merge.MergeCommand.CanExecute(null));
        var view = new PullRequestInspectionView { DataContext = model };
        var window = new Window { Width = 900, Height = 640, Content = new AppScrollViewer { Content = view } };
        window.Show();
        try
        {
            window.UpdateLayout();
            var button = Assert.Single(view.GetVisualDescendants().OfType<AppButton>(),
                item => Avalonia.Automation.AutomationProperties.GetName(item) == PullRequestText.MergePullRequest);
            Assert.False(button.IsEffectivelyVisible);
            Assert.Contains(view.GetVisualDescendants().OfType<AppText>(),
                text => text.Text == PullRequestText.MergeReadOnlyHint && text.IsEffectivelyVisible);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ClosedPullRequestsHideTheButtonWithoutHints() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestInspectionUiFixture.CreateAsync(canWrite: true);
        fixture.Source.Transport.RespondAsync = (request, token) =>
            request.RequestUri!.AbsolutePath.EndsWith("/pulls/7", StringComparison.Ordinal)
                ? Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent(PullRequestTestData.Item(state: "closed")) })
                : PullRequestInspectionUiFixture.RespondAsync(request, token);
        await fixture.Model.DetailCommand.ExecuteAsync(null);
        var merge = fixture.Model.Inspection.Merge;
        Assert.True(merge.IsActive);
        Assert.False(merge.IsOpen);
        Assert.False(merge.CanMerge);
        Assert.False(merge.ShowReadOnlyHint);
        Assert.False(merge.ShowDraftHint);
        Assert.False(merge.MergeCommand.CanExecute(null));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task DraftPullRequestsShowTheDraftHintAndNeverTheButton() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestInspectionUiFixture.CreateAsync(canWrite: true);
        fixture.Source.Transport.RespondAsync = (request, token) =>
            request.RequestUri!.AbsolutePath.EndsWith("/pulls/7", StringComparison.Ordinal)
                ? Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent(PullRequestTestData.Item(draft: true)) })
                : PullRequestInspectionUiFixture.RespondAsync(request, token);
        await fixture.Model.DetailCommand.ExecuteAsync(null);
        var merge = fixture.Model.Inspection.Merge;
        Assert.True(merge.IsActive);
        Assert.True(merge.IsOpen);
        Assert.True(merge.IsDraft);
        Assert.True(merge.CanWrite);
        Assert.False(merge.CanMerge);
        Assert.True(merge.ShowDraftHint);
        Assert.False(merge.ShowReadOnlyHint);
        Assert.False(merge.MergeCommand.CanExecute(null));
        var view = new PullRequestInspectionView { DataContext = fixture.Model.Inspection };
        var window = new Window { Width = 900, Height = 640, Content = new AppScrollViewer { Content = view } };
        window.Show();
        try
        {
            window.UpdateLayout();
            var button = Assert.Single(view.GetVisualDescendants().OfType<AppButton>(),
                item => Avalonia.Automation.AutomationProperties.GetName(item) == PullRequestText.MergePullRequest);
            Assert.False(button.IsEffectivelyVisible);
            Assert.Contains(view.GetVisualDescendants().OfType<AppText>(),
                text => text.Text == PullRequestText.MergeDraftHint && text.IsEffectivelyVisible);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task MethodIsRequiredBeforeAnyMergeAttempt() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestInspectionUiFixture.CreateAsync(canWrite: true);
        var puts = 0;
        fixture.Source.Transport.RespondAsync = (request, token) =>
        {
            if (request.Method == HttpMethod.Put)
            {
                puts++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent(MergeAccepted) });
            }
            return PullRequestInspectionUiFixture.RespondAsync(request, token);
        };
        var merge = fixture.Model.Inspection.Merge;
        Assert.True(merge.CanMerge);
        Assert.Null(merge.SelectedMethod);
        Assert.False(merge.MergeCommand.CanExecute(null));
        await merge.MergeCommand.ExecuteAsync(null);
        Assert.Equal(0, puts);
        merge.SelectedMethod = merge.Methods.Single(option => option.Method == PullRequestMergeMethod.Squash);
        Assert.True(merge.MergeCommand.CanExecute(null));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task WriteGrantPutsOnceAfterTheSecondConfirmTapAndReloadsDetail() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestInspectionUiFixture.CreateAsync(canWrite: true);
        var puts = 0;
        var put = false;
        string? payload = null;
        fixture.Source.Transport.RespondAsync = async (request, token) =>
        {
            if (request.Method == HttpMethod.Put)
            {
                puts++;
                put = true;
                payload = await request.Content!.ReadAsStringAsync(token);
                Assert.Equal("/repos/fixture/repo/pulls/7/merge", request.RequestUri!.AbsolutePath);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(MergeAccepted) };
            }
            return request.RequestUri!.AbsolutePath.EndsWith("/pulls/7", StringComparison.Ordinal)
                ? await Detail(put) : await PullRequestInspectionUiFixture.RespondAsync(request, token);
        };
        var model = fixture.Model;
        var merge = model.Inspection.Merge;
        merge.SelectedMethod = merge.Methods.Single(option => option.Method == PullRequestMergeMethod.Squash);
        Assert.True(merge.MergeCommand.CanExecute(null));

        await merge.MergeCommand.ExecuteAsync(null);
        Assert.Equal(0, puts);
        Assert.Equal(PullRequestText.MergeConfirm, merge.MergeLabel);
        Assert.Equal(PullRequestText.MergeConfirmHint, merge.Status);

        await merge.MergeCommand.ExecuteAsync(null);
        Assert.Equal(1, puts);
        var sent = System.Text.Json.JsonDocument.Parse(payload!).RootElement;
        Assert.Equal(new string('b', 40), sent.GetProperty("sha").GetString());
        Assert.Equal("squash", sent.GetProperty("merge_method").GetString());
        if (model.DetailCommand.ExecutionTask is { } reload) await reload;
        Assert.Equal("closed", model.Detail!.State);
        Assert.False(merge.IsOpen);
        Assert.False(merge.CanMerge);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task AmbiguousOutcomeReconcilesByMergedDetailWithoutReputting() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestInspectionUiFixture.CreateAsync(canWrite: true);
        var puts = 0;
        var put = false;
        fixture.Source.Transport.RespondAsync = (request, token) =>
        {
            if (request.Method == HttpMethod.Put)
            {
                puts++;
                put = true;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            }
            return request.RequestUri!.AbsolutePath.EndsWith("/pulls/7", StringComparison.Ordinal)
                ? Detail(put) : PullRequestInspectionUiFixture.RespondAsync(request, token);
        };
        var model = fixture.Model;
        var merge = model.Inspection.Merge;
        merge.SelectedMethod = merge.Methods.Single(option => option.Method == PullRequestMergeMethod.Merge);
        await merge.MergeCommand.ExecuteAsync(null);
        await merge.MergeCommand.ExecuteAsync(null);
        Assert.Equal(1, puts);
        if (model.DetailCommand.ExecutionTask is { } reload) await reload;
        Assert.Equal("closed", model.Detail!.State);
        Assert.False(merge.CanMerge);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task RejectedMergeShowsTheLocalizedFailureAndStaysMergeable() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestInspectionUiFixture.CreateAsync(canWrite: true);
        var puts = 0;
        fixture.Source.Transport.RespondAsync = (request, token) =>
        {
            if (request.Method != HttpMethod.Put) return PullRequestInspectionUiFixture.RespondAsync(request, token);
            puts++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.UnprocessableEntity));
        };
        var merge = fixture.Model.Inspection.Merge;
        merge.SelectedMethod = merge.Methods.Single(option => option.Method == PullRequestMergeMethod.Merge);
        await merge.MergeCommand.ExecuteAsync(null);
        await merge.MergeCommand.ExecuteAsync(null);
        Assert.Equal(1, puts);
        Assert.Equal(PullRequestText.Failure("source_write_rejected", "merge"), merge.Status);
        Assert.True(merge.CanMerge);
        Assert.Equal("open", fixture.Model.Detail!.State);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task CancellingAnInFlightMergeReportsTheHonestStatus() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestInspectionUiFixture.CreateAsync(canWrite: true);
        var puts = 0;
        var entered = new TaskCompletionSource();
        fixture.Source.Transport.RespondAsync = async (request, token) =>
        {
            if (request.Method != HttpMethod.Put) return await PullRequestInspectionUiFixture.RespondAsync(request, token);
            puts++;
            entered.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        };
        var merge = fixture.Model.Inspection.Merge;
        merge.SelectedMethod = merge.Methods.Single(option => option.Method == PullRequestMergeMethod.Rebase);
        await merge.MergeCommand.ExecuteAsync(null);
        var publish = merge.MergeCommand.ExecuteAsync(null);
        await entered.Task;
        merge.CancelCommand.Execute(null);
        await publish;
        Assert.Equal(1, puts);
        Assert.Equal(PullRequestText.MergeCancelled, merge.Status);
        Assert.True(merge.CanMerge);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ClearingMidflightInvalidatesTheGenerationAndDisposeDrains() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestInspectionUiFixture.CreateAsync(canWrite: true);
        var entered = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        fixture.Source.Transport.RespondAsync = async (request, token) =>
        {
            if (request.Method != HttpMethod.Put) return await PullRequestInspectionUiFixture.RespondAsync(request, token);
            entered.SetResult();
            await release.Task;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(MergeAccepted) };
        };
        var inspection = fixture.Model.Inspection;
        var merge = inspection.Merge;
        merge.SelectedMethod = merge.Methods.Single(option => option.Method == PullRequestMergeMethod.Merge);
        await merge.MergeCommand.ExecuteAsync(null);
        var publish = merge.MergeCommand.ExecuteAsync(null);
        await entered.Task;
        inspection.Clear();
        Assert.False(merge.IsActive);
        Assert.False(merge.CanMerge);
        Assert.Null(merge.SelectedMethod);
        release.SetResult();
        await publish;
        Assert.Equal(string.Empty, merge.Status);
        await merge.DisposeAsync();
        Assert.False(merge.MergeCommand.CanExecute(null));
    }, TestContext.Current.CancellationToken);
}
