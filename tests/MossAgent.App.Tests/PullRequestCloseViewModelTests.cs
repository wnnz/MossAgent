using System.Net;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Resources;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class PullRequestCloseViewModelTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    private static Task<HttpResponseMessage> Detail(bool patched) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent(PullRequestTestData.Item(state: patched ? "closed" : "open")) });

    [Fact]
    public Task ReadOnlyGrantShowsTheLocalizedHintAndKeepsTheCloseButtonHidden() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestInspectionUiFixture.CreateAsync();
        var model = fixture.Model.Inspection;
        Assert.True(model.Close.IsActive);
        Assert.False(model.Close.CanWrite);
        Assert.False(model.Close.CanClose);
        Assert.True(model.Close.ShowReadOnlyHint);
        Assert.False(model.Close.CloseCommand.CanExecute(null));
        var view = new PullRequestInspectionView { DataContext = model };
        var window = new Window { Width = 900, Height = 640, Content = new AppScrollViewer { Content = view } };
        window.Show();
        try
        {
            window.UpdateLayout();
            var button = Assert.Single(view.GetVisualDescendants().OfType<AppButton>(),
                item => Avalonia.Automation.AutomationProperties.GetName(item) == PullRequestText.ClosePullRequest);
            Assert.False(button.IsEffectivelyVisible);
            Assert.Contains(view.GetVisualDescendants().OfType<AppText>(),
                text => text.Text == PullRequestText.CloseReadOnlyHint && text.IsEffectivelyVisible);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ClosedPullRequestsHideTheButtonWithoutTheReadOnlyHint() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestInspectionUiFixture.CreateAsync(canWrite: true);
        fixture.Source.Transport.RespondAsync = (request, token) =>
            request.RequestUri!.AbsolutePath.EndsWith("/pulls/7", StringComparison.Ordinal)
                ? Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent(PullRequestTestData.Item(state: "closed")) })
                : PullRequestInspectionUiFixture.RespondAsync(request, token);
        await fixture.Model.DetailCommand.ExecuteAsync(null);
        var close = fixture.Model.Inspection.Close;
        Assert.True(close.IsActive);
        Assert.False(close.IsOpen);
        Assert.False(close.CanClose);
        Assert.False(close.ShowReadOnlyHint);
        Assert.False(close.CloseCommand.CanExecute(null));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task WriteGrantPatchesOnceAfterTheSecondConfirmTapAndReloadsDetail() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestInspectionUiFixture.CreateAsync(canWrite: true);
        var patches = 0;
        var patched = false;
        fixture.Source.Transport.RespondAsync = (request, token) =>
        {
            if (request.Method == HttpMethod.Patch)
            {
                patches++;
                patched = true;
                Assert.Equal("/repos/fixture/repo/pulls/7", request.RequestUri!.AbsolutePath);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent(PullRequestTestData.Item(state: "closed")) });
            }
            return request.RequestUri!.AbsolutePath.EndsWith("/pulls/7", StringComparison.Ordinal)
                ? Detail(patched) : PullRequestInspectionUiFixture.RespondAsync(request, token);
        };
        var model = fixture.Model;
        var close = model.Inspection.Close;
        Assert.True(close.CanClose);
        Assert.True(close.CloseCommand.CanExecute(null));

        await close.CloseCommand.ExecuteAsync(null);
        Assert.Equal(0, patches);
        Assert.Equal(PullRequestText.CloseConfirm, close.CloseLabel);
        Assert.Equal(PullRequestText.CloseConfirmHint, close.Status);

        await close.CloseCommand.ExecuteAsync(null);
        Assert.Equal(1, patches);
        if (model.DetailCommand.ExecutionTask is { } reload) await reload;
        Assert.Equal("closed", model.Detail!.State);
        Assert.False(close.IsOpen);
        Assert.False(close.CanClose);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task AmbiguousOutcomeReconcilesByClosedSummaryWithoutRepatching() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestInspectionUiFixture.CreateAsync(canWrite: true);
        var patches = 0;
        var patched = false;
        fixture.Source.Transport.RespondAsync = (request, token) =>
        {
            if (request.Method == HttpMethod.Patch)
            {
                patches++;
                patched = true;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            }
            return request.RequestUri!.AbsolutePath.EndsWith("/pulls/7", StringComparison.Ordinal)
                ? Detail(patched) : PullRequestInspectionUiFixture.RespondAsync(request, token);
        };
        var model = fixture.Model;
        var close = model.Inspection.Close;
        await close.CloseCommand.ExecuteAsync(null);
        await close.CloseCommand.ExecuteAsync(null);
        Assert.Equal(1, patches);
        if (model.DetailCommand.ExecutionTask is { } reload) await reload;
        Assert.Equal("closed", model.Detail!.State);
        Assert.False(close.CanClose);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task RejectedCloseShowsTheLocalizedFailureAndStaysOpen() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestInspectionUiFixture.CreateAsync(canWrite: true);
        var patches = 0;
        fixture.Source.Transport.RespondAsync = (request, token) =>
        {
            if (request.Method != HttpMethod.Patch) return PullRequestInspectionUiFixture.RespondAsync(request, token);
            patches++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.UnprocessableEntity));
        };
        var close = fixture.Model.Inspection.Close;
        await close.CloseCommand.ExecuteAsync(null);
        await close.CloseCommand.ExecuteAsync(null);
        Assert.Equal(1, patches);
        Assert.Equal(PullRequestText.Failure("source_write_rejected", "close"), close.Status);
        Assert.True(close.CanClose);
        Assert.Equal("open", fixture.Model.Detail!.State);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task CancellingAnInFlightCloseReportsTheHonestStatus() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestInspectionUiFixture.CreateAsync(canWrite: true);
        var patches = 0;
        var entered = new TaskCompletionSource();
        fixture.Source.Transport.RespondAsync = async (request, token) =>
        {
            if (request.Method != HttpMethod.Patch) return await PullRequestInspectionUiFixture.RespondAsync(request, token);
            patches++;
            entered.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        };
        var close = fixture.Model.Inspection.Close;
        await close.CloseCommand.ExecuteAsync(null);
        var publish = close.CloseCommand.ExecuteAsync(null);
        await entered.Task;
        close.CancelCommand.Execute(null);
        await publish;
        Assert.Equal(1, patches);
        Assert.Equal(PullRequestText.CloseCancelled, close.Status);
        Assert.True(close.CanClose);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ClearingMidflightInvalidatesTheGenerationAndDisposeDrains() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestInspectionUiFixture.CreateAsync(canWrite: true);
        var entered = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        fixture.Source.Transport.RespondAsync = async (request, token) =>
        {
            if (request.Method != HttpMethod.Patch) return await PullRequestInspectionUiFixture.RespondAsync(request, token);
            entered.SetResult();
            await release.Task;
            return new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(PullRequestTestData.Item(state: "closed")) };
        };
        var inspection = fixture.Model.Inspection;
        var close = inspection.Close;
        await close.CloseCommand.ExecuteAsync(null);
        var publish = close.CloseCommand.ExecuteAsync(null);
        await entered.Task;
        inspection.Clear();
        Assert.False(close.IsActive);
        Assert.False(close.CanClose);
        release.SetResult();
        await publish;
        Assert.Equal(string.Empty, close.Status);
        await close.DisposeAsync();
        Assert.False(close.CloseCommand.CanExecute(null));
    }, TestContext.Current.CancellationToken);
}
