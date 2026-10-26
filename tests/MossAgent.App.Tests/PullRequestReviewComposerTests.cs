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
public sealed class PullRequestReviewComposerTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    private static void Writes(PullRequestInspectionUiFixture fixture,
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> post) =>
        fixture.Source.Transport.RespondAsync = (request, token) => request.Method == HttpMethod.Post
            ? post(request, token) : PullRequestInspectionUiFixture.RespondAsync(request, token);

    [Fact]
    public Task ReadOnlyGrantShowsTheLocalizedHintAndKeepsTheComposerHidden() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestInspectionUiFixture.CreateAsync();
        var model = fixture.Model.Inspection;
        Assert.True(model.ReviewComposer.IsActive);
        Assert.False(model.ReviewComposer.CanWrite);
        Assert.False(model.ReviewComposer.PublishCommand.CanExecute(null));
        var view = new PullRequestInspectionView { DataContext = model };
        var window = new Window { Width = 900, Height = 640, Content = new AppScrollViewer { Content = view } };
        window.Show();
        try
        {
            model.SelectedSection = model.Sections.Single(section => section.Key == "reviews");
            window.UpdateLayout();
            var input = Assert.Single(view.GetVisualDescendants().OfType<AppInput>(),
                item => Avalonia.Automation.AutomationProperties.GetName(item) == PullRequestText.ReviewBody);
            Assert.False(input.IsEffectivelyVisible);
            Assert.Contains(view.GetVisualDescendants().OfType<AppText>(),
                text => text.Text == PullRequestText.ReviewReadOnlyHint && text.IsEffectivelyVisible);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task DecisionIsRequiredBeforeAnyPublishAttempt() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestInspectionUiFixture.CreateAsync(canWrite: true);
        var posts = 0;
        Writes(fixture, (_, _) =>
        {
            posts++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created));
        });
        var composer = fixture.Model.Inspection.ReviewComposer;
        Assert.True(composer.CanWrite);
        Assert.Null(composer.SelectedDecision);
        Assert.False(composer.PublishCommand.CanExecute(null));
        await composer.PublishCommand.ExecuteAsync(null);
        Assert.Equal(0, posts);
        composer.SelectedDecision = composer.Decisions.Single(option => option.Decision == PullRequestReviewDecision.Approve);
        Assert.True(composer.PublishCommand.CanExecute(null));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task WriteGrantPostsExactlyOnceAfterTheSecondConfirmTap() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestInspectionUiFixture.CreateAsync(canWrite: true);
        var posts = 0;
        Writes(fixture, (request, _) =>
        {
            posts++;
            Assert.Equal("/repos/fixture/repo/pulls/7/reviews", request.RequestUri!.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created)
            { Content = new StringContent(PullRequestTestData.Review(body: null)) });
        });
        var model = fixture.Model.Inspection;
        var composer = model.ReviewComposer;
        composer.SelectedDecision = composer.Decisions.Single(option => option.Decision == PullRequestReviewDecision.Approve);
        composer.Text = "ship it";
        Assert.True(composer.PublishCommand.CanExecute(null));

        await composer.PublishCommand.ExecuteAsync(null);
        Assert.Equal(0, posts);
        Assert.Equal(PullRequestText.ReviewConfirm, composer.PublishLabel);
        Assert.Equal(PullRequestText.ReviewConfirmHint, composer.Status);

        await composer.PublishCommand.ExecuteAsync(null);
        Assert.Equal(1, posts);
        Assert.Equal(string.Empty, composer.Text);
        Assert.Null(composer.SelectedDecision);
        Assert.Equal(PullRequestText.ReviewSubmitted, composer.Status);
        if (model.Reviews.LoadCommand.ExecutionTask is { } reload) await reload;
        Assert.Single(model.Reviews.Items);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task AmbiguousOutcomeReconcilesWithoutRepostingAndReportsTheCaveat() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestInspectionUiFixture.CreateAsync(canWrite: true);
        var posts = 0;
        fixture.Source.Transport.RespondAsync = (request, token) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post)
            {
                posts++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            }
            if (path.EndsWith("/reviews", StringComparison.Ordinal))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent($"[{PullRequestTestData.Review(state: "COMMENTED", body: "landed?")}]") });
            return PullRequestInspectionUiFixture.RespondAsync(request, token);
        };
        var composer = fixture.Model.Inspection.ReviewComposer;
        composer.SelectedDecision = composer.Decisions.Single(option => option.Decision == PullRequestReviewDecision.Comment);
        composer.Text = "landed?";
        await composer.PublishCommand.ExecuteAsync(null);
        await composer.PublishCommand.ExecuteAsync(null);
        Assert.Equal(1, posts);
        Assert.Equal(PullRequestText.ReviewLanded, composer.Status);
        Assert.Equal(string.Empty, composer.Text);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task RejectedWriteKeepsTheDraftAndShowsTheLocalizedFailure() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestInspectionUiFixture.CreateAsync(canWrite: true);
        var posts = 0;
        Writes(fixture, (_, _) =>
        {
            posts++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.UnprocessableEntity));
        });
        var composer = fixture.Model.Inspection.ReviewComposer;
        composer.SelectedDecision = composer.Decisions.Single(option => option.Decision == PullRequestReviewDecision.RequestChanges);
        composer.Text = "needs work";
        await composer.PublishCommand.ExecuteAsync(null);
        await composer.PublishCommand.ExecuteAsync(null);
        Assert.Equal(1, posts);
        Assert.Equal("needs work", composer.Text);
        Assert.NotNull(composer.SelectedDecision);
        Assert.Equal(PullRequestText.Failure("source_write_rejected", "review"), composer.Status);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task CancellingAnInFlightWriteStopsWithoutRepostingAndKeepsTheDraft() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestInspectionUiFixture.CreateAsync(canWrite: true);
        var posts = 0;
        var entered = new TaskCompletionSource();
        Writes(fixture, async (_, token) =>
        {
            posts++;
            entered.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            return new HttpResponseMessage(HttpStatusCode.Created);
        });
        var composer = fixture.Model.Inspection.ReviewComposer;
        composer.SelectedDecision = composer.Decisions.Single(option => option.Decision == PullRequestReviewDecision.Approve);
        composer.Text = "ship it";
        await composer.PublishCommand.ExecuteAsync(null);
        var publish = composer.PublishCommand.ExecuteAsync(null);
        await entered.Task;
        composer.CancelCommand.Execute(null);
        await publish;
        Assert.Equal(1, posts);
        Assert.Equal("ship it", composer.Text);
        Assert.Equal(PullRequestText.ReviewCancelled, composer.Status);
    }, TestContext.Current.CancellationToken);
}
