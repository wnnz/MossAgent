using System.Net;
using System.Text.Json;
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
public sealed class PullRequestCreateViewModelTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    private static Task<HttpResponseMessage> Branches() => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
    { Content = new StringContent($"[{PullRequestTestData.Branch("main")},{PullRequestTestData.Branch("feature")}]") });

    private static Task<HttpResponseMessage> Fallthrough(HttpRequestMessage request, CancellationToken token)
    {
        var path = request.RequestUri!.AbsolutePath;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent(path.EndsWith("/pulls/9", StringComparison.Ordinal)
            ? PullRequestTestData.Item(number: 9)
            : request.RequestUri.Query.Length > 0 ? $"[{PullRequestTestData.Item()}]" : PullRequestTestData.Item()) });
    }

    [Fact]
    public Task ReadOnlyGrantKeepsTheCreateActionHiddenAndInert() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestInspectionUiFixture.CreateAsync(canWrite: false);
        var model = fixture.Model;
        Assert.False(model.CanCreatePr);
        Assert.False(model.CreatePullRequestCommand.CanExecute(null));
        await model.CreatePullRequestCommand.ExecuteAsync(null);
        Assert.False(model.IsCreatePrOpen);
        var view = new PullRequestsView { DataContext = model };
        var window = new Window { Width = 960, Height = 700, Content = view };
        window.Show();
        try
        {
            window.UpdateLayout();
            var button = Assert.Single(view.GetVisualDescendants().OfType<AppActionButton>(),
                item => item.Label == PullRequestText.CreatePullRequest);
            Assert.False(button.IsEffectivelyVisible);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task OpenLoadsBranchesAndRequiresExplicitBaseAndHead() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestInspectionUiFixture.CreateAsync(canWrite: true);
        fixture.Source.Transport.RespondAsync = (request, token) =>
            request.RequestUri!.AbsolutePath.EndsWith("/branches", StringComparison.Ordinal)
                ? Branches() : Fallthrough(request, token);
        var model = fixture.Model;
        Assert.True(model.CanCreatePr);

        await model.CreatePullRequestCommand.ExecuteAsync(null);
        var create = model.CreatePr;
        Assert.True(model.IsCreatePrOpen);
        Assert.True(create.IsOpen);
        Assert.Equal(["feature", "main"], create.Branches.Select(branch => branch.Name).Order().ToArray());
        Assert.Null(create.SelectedBase);
        Assert.Null(create.SelectedHead);
        Assert.False(create.SubmitCommand.CanExecute(null));

        create.SelectedBase = create.Branches.First(branch => branch.Name == "main");
        create.SelectedHead = create.Branches.First(branch => branch.Name == "feature");
        create.Title = "Add feature";
        Assert.True(create.SubmitCommand.CanExecute(null));
        // Same branch for base and head is never submittable.
        create.SelectedHead = create.SelectedBase;
        Assert.False(create.SubmitCommand.CanExecute(null));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task BranchListFailureReportsTheLocalizedStatusAndKeepsSubmitInert() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestInspectionUiFixture.CreateAsync(canWrite: true);
        fixture.Source.Transport.RespondAsync = (request, token) =>
            request.RequestUri!.AbsolutePath.EndsWith("/branches", StringComparison.Ordinal)
                ? Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)) : Fallthrough(request, token);

        await fixture.Model.CreatePullRequestCommand.ExecuteAsync(null);
        var create = fixture.Model.CreatePr;
        Assert.True(create.IsOpen);
        Assert.Empty(create.Branches);
        Assert.Equal(PullRequestText.BranchesFailed, create.Status);
        Assert.False(create.SubmitCommand.CanExecute(null));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task TwoStageConfirmPostsOnceAndOpensTheCreatedDetail() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestInspectionUiFixture.CreateAsync(canWrite: true);
        var posts = 0;
        string? payload = null;
        fixture.Source.Transport.RespondAsync = async (request, token) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post && path.EndsWith("/pulls", StringComparison.Ordinal))
            {
                posts++;
                payload = await request.Content!.ReadAsStringAsync(token);
                return new HttpResponseMessage(HttpStatusCode.Created)
                { Content = new StringContent(PullRequestTestData.CreatedItem()) };
            }
            return path.EndsWith("/branches", StringComparison.Ordinal) ? await Branches() : await Fallthrough(request, token);
        };
        var model = fixture.Model;
        await model.CreatePullRequestCommand.ExecuteAsync(null);
        var create = model.CreatePr;
        create.SelectedBase = create.Branches.First(branch => branch.Name == "main");
        create.SelectedHead = create.Branches.First(branch => branch.Name == "feature");
        create.Title = "Add feature";
        Assert.True(create.SubmitCommand.CanExecute(null));

        await create.SubmitCommand.ExecuteAsync(null);
        Assert.Equal(0, posts);
        Assert.Equal(PullRequestText.CreatePrConfirm, create.SubmitLabel);
        Assert.Equal(PullRequestText.CreatePrConfirmHint, create.Status);

        await create.SubmitCommand.ExecuteAsync(null);
        Assert.Equal(1, posts);
        var sent = JsonDocument.Parse(payload!).RootElement;
        Assert.Equal("Add feature", sent.GetProperty("title").GetString());
        Assert.Equal("feature", sent.GetProperty("head").GetString());
        Assert.Equal("main", sent.GetProperty("base").GetString());
        Assert.False(sent.GetProperty("draft").GetBoolean());
        Assert.False(sent.TryGetProperty("body", out _));
        Assert.False(create.IsOpen);
        Assert.False(model.IsCreatePrOpen);
        Assert.Equal(9, model.Detail!.Number);
        Assert.Contains(model.Items, item => item.Summary.Number == 9);
        Assert.Equal(PullRequestText.PrCreated, model.Status);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task DraftFlagAndBodyReachTheWireUnchanged() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestInspectionUiFixture.CreateAsync(canWrite: true);
        string? payload = null;
        fixture.Source.Transport.RespondAsync = async (request, token) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post && path.EndsWith("/pulls", StringComparison.Ordinal))
            {
                payload = await request.Content!.ReadAsStringAsync(token);
                return new HttpResponseMessage(HttpStatusCode.Created)
                { Content = new StringContent(PullRequestTestData.CreatedItem()) };
            }
            return path.EndsWith("/branches", StringComparison.Ordinal) ? await Branches() : await Fallthrough(request, token);
        };
        await fixture.Model.CreatePullRequestCommand.ExecuteAsync(null);
        var create = fixture.Model.CreatePr;
        create.SelectedBase = create.Branches.First(branch => branch.Name == "main");
        create.SelectedHead = create.Branches.First(branch => branch.Name == "feature");
        create.Title = "Draft work";
        create.Body = "notes";
        create.IsDraft = true;

        await create.SubmitCommand.ExecuteAsync(null);
        await create.SubmitCommand.ExecuteAsync(null);
        var sent = JsonDocument.Parse(payload!).RootElement;
        Assert.True(sent.GetProperty("draft").GetBoolean());
        Assert.Equal("notes", sent.GetProperty("body").GetString());
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task RejectedCreateKeepsTheDialogOpenWithTheLocalizedStatus() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestInspectionUiFixture.CreateAsync(canWrite: true);
        fixture.Source.Transport.RespondAsync = (request, token) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post && path.EndsWith("/pulls", StringComparison.Ordinal))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.UnprocessableEntity));
            return path.EndsWith("/branches", StringComparison.Ordinal) ? Branches() : Fallthrough(request, token);
        };
        await fixture.Model.CreatePullRequestCommand.ExecuteAsync(null);
        var create = fixture.Model.CreatePr;
        create.SelectedBase = create.Branches.First(branch => branch.Name == "main");
        create.SelectedHead = create.Branches.First(branch => branch.Name == "feature");
        create.Title = "Add feature";

        await create.SubmitCommand.ExecuteAsync(null);
        await create.SubmitCommand.ExecuteAsync(null);
        Assert.True(create.IsOpen);
        Assert.Equal(PullRequestText.Failure("source_write_rejected", "create"), create.Status);
        Assert.True(create.SubmitCommand.CanExecute(null));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task CancellingAnInFlightCreateReportsTheHonestStatus() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestInspectionUiFixture.CreateAsync(canWrite: true);
        var posts = 0;
        var entered = new TaskCompletionSource();
        fixture.Source.Transport.RespondAsync = async (request, token) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post && path.EndsWith("/pulls", StringComparison.Ordinal))
            {
                posts++;
                entered.SetResult();
                await Task.Delay(Timeout.Infinite, token);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            return path.EndsWith("/branches", StringComparison.Ordinal) ? await Branches() : await Fallthrough(request, token);
        };
        await fixture.Model.CreatePullRequestCommand.ExecuteAsync(null);
        var create = fixture.Model.CreatePr;
        create.SelectedBase = create.Branches.First(branch => branch.Name == "main");
        create.SelectedHead = create.Branches.First(branch => branch.Name == "feature");
        create.Title = "Add feature";
        await create.SubmitCommand.ExecuteAsync(null);
        var publish = create.SubmitCommand.ExecuteAsync(null);
        await entered.Task;
        create.CancelCommand.Execute(null);
        await publish;
        Assert.Equal(1, posts);
        Assert.Equal(PullRequestText.CreatePrCancelled, create.Status);
        Assert.True(create.IsOpen);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task DisposeDrainsAndDisablesCommands() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestInspectionUiFixture.CreateAsync(canWrite: true);
        fixture.Source.Transport.RespondAsync = (request, token) =>
            request.RequestUri!.AbsolutePath.EndsWith("/branches", StringComparison.Ordinal)
                ? Branches() : Fallthrough(request, token);
        await fixture.Model.CreatePullRequestCommand.ExecuteAsync(null);
        var create = fixture.Model.CreatePr;
        Assert.True(create.IsOpen);
        await create.DisposeAsync();
        Assert.False(create.IsOpen);
        Assert.False(create.SubmitCommand.CanExecute(null));
        Assert.False(fixture.Model.CreatePullRequestCommand.CanExecute(null));
    }, TestContext.Current.CancellationToken);
}
